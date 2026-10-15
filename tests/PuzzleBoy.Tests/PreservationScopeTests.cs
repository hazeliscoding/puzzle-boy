using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using PuzzleBoy.Rendering;
using PuzzleBoy.Rules;

namespace PuzzleBoy.Tests;

// This port plays the original's 20 stages and nothing else. The bonus stages and the switch moved to Community Edition.
public sealed class PreservationScopeTests
{
    // The 23 unused files M4 played after stage 20 (ROADMAP.md, M4).
    private static readonly int[] BonusFiles = [1, 30, 31, 0, 3, 9, 35, 43, 5, 6, 33, 7, 19, 47, 39, 41, 45, 34, 12, 46, 15, 11, 44];

    [Fact]
    public void The_original_order_is_exactly_the_twenty_documented_files()
    {
        Assert.Equal([2, 4, 32, 8, 38, 36, 37, 48, 42, 49, 40, 20, 22, 13, 21, 24, 14, 10, 25, 26], OriginalStageOrder.Files);
    }

    [Fact]
    public void The_original_order_plays_each_file_once()
    {
        Assert.Equal(OriginalStageOrder.Files.Count, OriginalStageOrder.Files.Distinct().Count());
    }

    [Fact]
    public void The_original_order_holds_no_bonus_file()
    {
        Assert.Empty(OriginalStageOrder.Files.Intersect(BonusFiles));
    }

    [Fact]
    public void The_game_loads_only_the_original_stages_when_the_bundle_holds_all_fifty()
    {
        var file = Ascii.Stage("1.G").ToFile();
        var text = Enumerable.Range(0, 50).ToDictionary(OriginalStageOrder.AssetName, _ => file);

        Assert.Equal(OriginalStageOrder.Files, GameAssets.ReadStages(text).Select(stage => stage.File));
    }

    [Fact]
    public void The_preservation_game_never_calls_Board_Switch()
    {
        Assert.DoesNotContain(nameof(Board.Switch), BoardMembersTheGameUses());
    }

    // Positive control: the scan does find a Board member the game really calls.
    [Fact]
    public void The_member_scan_finds_the_game_calling_Board_Step()
    {
        Assert.Contains(nameof(Board.Step), BoardMembersTheGameUses());
    }

    /// <summary>
    /// The names of the <see cref="Board"/> members the game's assembly and the engine's rendering library reference,
    /// read from their metadata.
    /// </summary>
    private static List<string> BoardMembersTheGameUses()
    {
        var names = new List<string>();
        foreach (var assembly in new[] { typeof(PuzzleBoyGame).Assembly, typeof(BoardView).Assembly })
        {
            using var pe = new PEReader(File.OpenRead(assembly.Location));
            var metadata = pe.GetMetadataReader();
            foreach (var handle in metadata.MemberReferences)
            {
                var member = metadata.GetMemberReference(handle);
                if (member.Parent.Kind != HandleKind.TypeReference)
                {
                    continue;
                }

                var type = metadata.GetTypeReference((TypeReferenceHandle)member.Parent);
                if (metadata.GetString(type.Namespace) == typeof(Board).Namespace && metadata.GetString(type.Name) == nameof(Board))
                {
                    names.Add(metadata.GetString(member.Name));
                }
            }
        }

        return names;
    }
}
