using EHC.Web.Composers;

public class SeedPhotosTests
{
    [Fact]
    public void Every_facility_photo_is_in_the_build() =>
        Assert.All(FacilityPhotosSeeder.Photos.Values.Distinct(), slug => Assert.True(SeedPhotos.Exists("facility-" + slug + ".jpg"), slug));

    [Fact]
    public void Every_article_photo_is_in_the_build() =>
        Assert.All(HealthArticleImagesSeeder.Photos, p => Assert.True(SeedPhotos.Exists(p.File), p.File));
}
