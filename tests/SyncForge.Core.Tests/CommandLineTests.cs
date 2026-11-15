using SyncForge.Core.Cli;

namespace SyncForge.Core.Tests;

public class CommandLineTests
{
    [Fact]
    public void NoArguments_IsNormalStart()
    {
        Assert.False(CommandLineOptions.Parse([]).IsCommand);
        Assert.False(CommandLineOptions.Parse(["--background"]).IsCommand); // Autostart bleibt unberührt
    }

    [Fact]
    public void Run_CollectsProfiles()
    {
        var o = CommandLineOptions.Parse(["--run", "Dokumente", "--run", "Fotos auf NAS"]);
        Assert.Equal(CommandKind.Run, o.Kind);
        Assert.Equal(["Dokumente", "Fotos auf NAS"], o.Profiles);
    }

    [Fact]
    public void Run_WithoutName_IsInvalid()
    {
        Assert.Equal(CommandKind.Invalid, CommandLineOptions.Parse(["--run"]).Kind);
        Assert.Equal(CommandKind.Invalid, CommandLineOptions.Parse(["--run", "--list"]).Kind);
    }

    [Theory]
    [InlineData("--run-all", CommandKind.RunAll)]
    [InlineData("--list", CommandKind.List)]
    [InlineData("--help", CommandKind.Help)]
    [InlineData("/?", CommandKind.Help)]
    [InlineData("-R", CommandKind.Invalid)] // -r ohne Namen
    public void Kinds(string arg, CommandKind expected) => Assert.Equal(expected, CommandLineOptions.Parse([arg]).Kind);

    [Fact]
    public void RunAll_Wins_OverSingleRuns() =>
        Assert.Equal(CommandKind.RunAll, CommandLineOptions.Parse(["--run", "A", "--run-all"]).Kind);

    [Fact]
    public void Split_HandlesQuotes()
    {
        Assert.Equal(["--run", "Fotos auf NAS", "--run", "A"], CommandLineOptions.Split("--run \"Fotos auf NAS\" --run A"));
        Assert.Equal(["x", ""], CommandLineOptions.Split("x \"\""));
        Assert.Empty(CommandLineOptions.Split("   "));
    }
}
