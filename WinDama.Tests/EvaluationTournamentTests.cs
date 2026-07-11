using System.IO;
using System.Linq;
using NUnit.Framework;
using WinDama.Core;

namespace WinDama.Tests;

[TestFixture]
public sealed class EvaluationTournamentTests
{
    [Test]
    public void EvaluationTournamentRunner_PlaysSwappedColorGames()
    {
        EvaluationProfile balanced = new EvaluationProfile("balanced", EvaluationWeights.Default);
        EvaluationProfile aggressive = new EvaluationProfile("aggressive", new EvaluationWeights
        {
            ManValue = EvaluationWeights.Default.ManValue,
            DamaValue = EvaluationWeights.Default.DamaValue,
            WinLikeCaptureSwing = EvaluationWeights.Default.WinLikeCaptureSwing + 20,
            MobilityWeight = EvaluationWeights.Default.MobilityWeight,
            DamaMobilityWeight = EvaluationWeights.Default.DamaMobilityWeight,
            CenterManBonus = EvaluationWeights.Default.CenterManBonus,
            CenterDamaBonus = EvaluationWeights.Default.CenterDamaBonus,
            EdgeSafetyBonus = EvaluationWeights.Default.EdgeSafetyBonus,
            BackRowGuardBonus = EvaluationWeights.Default.BackRowGuardBonus,
            AdvancedManWeight = EvaluationWeights.Default.AdvancedManWeight,
            PromotionThreatBonus = EvaluationWeights.Default.PromotionThreatBonus,
            NearPromotionBonus = EvaluationWeights.Default.NearPromotionBonus,
            VulnerableManPenalty = EvaluationWeights.Default.VulnerableManPenalty,
            VulnerableDamaPenalty = EvaluationWeights.Default.VulnerableDamaPenalty,
            ProtectedManBonus = EvaluationWeights.Default.ProtectedManBonus,
            TempoBonus = EvaluationWeights.Default.TempoBonus,
            BestCaptureSequenceBonus = EvaluationWeights.Default.BestCaptureSequenceBonus + 20
        });

        EvaluationTournamentRunner runner = new EvaluationTournamentRunner();
        EvaluationTournamentSummary summary = runner.Run(balanced, aggressive, new EvaluationTournamentOptions
        {
            Depth = 1,
            ForcedCaptureComparisonDepth = 1,
            GamesPerColor = 1,
            MaxPliesPerGame = 6,
            MaxMovesWithoutCaptureOrPromotion = 6,
            UseQuiescenceSearch = false
        });

        Assert.That(summary.GameCount, Is.EqualTo(2));
        Assert.That(summary.Games[0].PlayerOneProfile, Is.EqualTo("balanced"));
        Assert.That(summary.Games[0].PlayerTwoProfile, Is.EqualTo("aggressive"));
        Assert.That(summary.Games[1].PlayerOneProfile, Is.EqualTo("aggressive"));
        Assert.That(summary.Games[1].PlayerTwoProfile, Is.EqualTo("balanced"));
        Assert.That(summary.Profiles.Count, Is.EqualTo(2));
        Assert.That(summary.Games.All(game => game.PlyCount > 0), Is.True);
        Assert.That(summary.Games.All(game => !string.IsNullOrWhiteSpace(game.MoveLog)), Is.True);
    }

    [Test]
    public void EvaluationTournamentSummary_CsvAndGameLogAreSaved()
    {
        EvaluationProfile profileA = new EvaluationProfile("A", EvaluationWeights.Default);
        EvaluationProfile profileB = new EvaluationProfile("B", EvaluationWeights.Default);
        EvaluationTournamentRunner runner = new EvaluationTournamentRunner();
        EvaluationTournamentSummary summary = runner.Run(profileA, profileB, new EvaluationTournamentOptions
        {
            Depth = 1,
            ForcedCaptureComparisonDepth = 1,
            GamesPerColor = 1,
            MaxPliesPerGame = 4,
            MaxMovesWithoutCaptureOrPromotion = 4,
            UseQuiescenceSearch = false
        });

        string csvPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "profile-tournament.csv");
        string logPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "profile-tournament-games.txt");

        summary.SaveCsv(csvPath);
        summary.SaveGameLog(logPath);

        Assert.That(File.Exists(csvPath), Is.True);
        Assert.That(File.Exists(logPath), Is.True);
        Assert.That(File.ReadAllText(csvPath), Does.Contain("player1,player2,result"));
        Assert.That(File.ReadAllText(logPath), Does.Contain("Tournament"));
    }

    [Test]
    public void EvaluationTournamentRunner_CanUseBundledProfiles()
    {
        var profiles = EvaluationProfileStore.LoadProfiles(EvaluationProfileDirectory());
        Assume.That(profiles.Count, Is.GreaterThanOrEqualTo(2));

        EvaluationTournamentRunner runner = new EvaluationTournamentRunner();
        EvaluationTournamentSummary summary = runner.Run(profiles[0], profiles[1], new EvaluationTournamentOptions
        {
            Depth = 1,
            ForcedCaptureComparisonDepth = 1,
            GamesPerColor = 1,
            MaxPliesPerGame = 4,
            MaxMovesWithoutCaptureOrPromotion = 4,
            UseQuiescenceSearch = false
        });

        Assert.That(summary.GameCount, Is.EqualTo(2));
        Assert.That(summary.Winner, Is.Not.Null);
        Assert.That(summary.Profiles.Sum(profile => profile.Games), Is.EqualTo(4));
    }

    [Test]
    public void EvaluationTournamentRunner_CanCompareHandcraftedAndLinearProfiles()
    {
        EvaluationProfile handcrafted = new EvaluationProfile("handcrafted", EvaluationWeights.Default);
        LinearEvaluationModel model = LinearEvaluationModel.FromEvaluationWeights(EvaluationWeights.Default, "linear-seed");
        EvaluationProfile learned = new EvaluationProfile("learned-linear", model);

        EvaluationTournamentRunner runner = new EvaluationTournamentRunner();
        EvaluationTournamentSummary summary = runner.Run(handcrafted, learned, new EvaluationTournamentOptions
        {
            Depth = 1,
            ForcedCaptureComparisonDepth = 1,
            GamesPerColor = 1,
            MaxPliesPerGame = 4,
            MaxMovesWithoutCaptureOrPromotion = 4,
            UseQuiescenceSearch = false
        });

        Assert.That(summary.GameCount, Is.EqualTo(2));
        Assert.That(summary.Games.Any(game => game.PlayerOneProfile == "learned-linear" || game.PlayerTwoProfile == "learned-linear"), Is.True);
        Assert.That(summary.Profiles.Any(profile => profile.ProfileName == "learned-linear"), Is.True);
    }

    private static string EvaluationProfileDirectory()
    {
        string direct = Path.Combine(TestContext.CurrentContext.TestDirectory, "EvaluationWeights");
        if (Directory.Exists(direct))
        {
            return direct;
        }

        DirectoryInfo? current = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (current != null)
        {
            string candidate = Path.Combine(current.FullName, "EvaluationWeights");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate EvaluationWeights.");
    }
}
