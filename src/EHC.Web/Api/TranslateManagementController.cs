using Asp.Versioning;
using EHC.Web.Blocks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Actions;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Security.Authorization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Extensions;

namespace EHC.Web.Api;

public sealed record CopyLanguageRequest(Guid DocumentKey, string From, string To);

public sealed record CopyLanguageResult(int Fields, int Blocks, int Skipped, bool NameCopied);

/// <summary>
/// Backoffice: "Fill empty Arabic from English" (and back) on a page — /umbraco/management/api/v1/ehc/translate/copy-missing.
/// Copies every text that is filled in one language and empty in the other (page fields, and inside sections, slides,
/// cards… at any depth), and shows those blocks in the other language too. Never overwrites a filled value. Saves a
/// draft only: nothing is published. The user needs update rights on the page in the target language.
/// </summary>
[ApiVersion("1.0")]
[VersionedApiBackOfficeRoute("ehc/translate")]
[ApiExplorerSettings(GroupName = "EHC")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessContent)]
public sealed class TranslateManagementController(
    IContentService contents,
    ILanguageService languages,
    IAuthorizationService authorization,
    IBackOfficeSecurityAccessor security) : ManagementApiControllerBase
{
    private static readonly HashSet<string> BlockEditors = new(StringComparer.Ordinal)
    {
        Constants.PropertyEditors.Aliases.BlockList, Constants.PropertyEditors.Aliases.BlockGrid,
    };

    [HttpPost("copy-missing")]
    public async Task<IActionResult> CopyMissing(CopyLanguageRequest request)
    {
        var known = (await languages.GetAllAsync()).Select(l => l.IsoCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (request.From == request.To || !known.Contains(request.From) || !known.Contains(request.To)) return BadRequest("Unknown languages.");

        var allowed = await authorization.AuthorizeResourceAsync(User,
            ContentPermissionResource.WithKeys(ActionUpdate.ActionLetter, request.DocumentKey, [request.To]),
            AuthorizationPolicies.ContentPermissionByResource);
        if (!allowed.Succeeded) return Forbid();
        if (security.BackOfficeSecurity?.CurrentUser is not { } user) return Unauthorized();

        if (contents.GetById(request.DocumentKey) is not { } page) return NotFound();
        if (!page.ContentType.VariesByCulture()) return BadRequest("This page has one version for all languages.");

        int fields = 0, blocks = 0, skipped = 0;
        var nameCopied = false;
        if (string.IsNullOrWhiteSpace(page.GetCultureName(request.To)) && page.GetCultureName(request.From) is { Length: > 0 } name)
        {
            page.SetCultureName(name, request.To);
            nameCopied = true;
        }

        foreach (var property in page.Properties)
        {
            var type = property.PropertyType;
            var isBlocks = BlockEditors.Contains(type.PropertyEditorAlias);
            if (type.VariesByCulture())
            {
                var source = property.GetValue(request.From);
                if (IsEmpty(source) || !IsEmpty(property.GetValue(request.To))) continue;
                // a language's own block list would share block keys with the copy: leave those to the editor
                if (isBlocks || (source is string s && s.Contains("\"contentData\"", StringComparison.Ordinal) && !s.Contains("\"contentData\":[]", StringComparison.Ordinal)))
                {
                    skipped++;
                    continue;
                }
                page.SetValue(type.Alias, source, request.To);
                fields++;
            }
            else if (isBlocks && BlockLanguages.CopyMissing(property.GetValue() as string, request.From, request.To) is { } result)
            {
                page.SetValue(type.Alias, result.Json);
                fields += result.Values;
                blocks += result.Blocks;
            }
        }

        if (fields > 0 || blocks > 0 || nameCopied)
        {
            contents.Save(page, user.Id);   // a draft, recorded under the editor's name
        }
        return Ok(new CopyLanguageResult(fields, blocks, skipped, nameCopied));
    }

    private static bool IsEmpty(object? value) => value switch
    {
        null => true,
        string s => string.IsNullOrWhiteSpace(s) || s.Trim() is "[]" or "{}",
        _ => false,
    };
}
