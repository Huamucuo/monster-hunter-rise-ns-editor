using System.Collections.ObjectModel;
using System.Security.Cryptography;
namespace FreshFoundation;

public sealed record EquipmentTemplate(
    int RecordIndex,
    uint Type,
    uint Id,
    string SourceDigest,
    string StateDigest,
    IReadOnlyList<uint> Decorations,
    IReadOnlyList<uint> OriginalDecorations,
    IReadOnlyList<SkillPair>? Pairs);

public sealed record EquipmentAddPiece(
    BuildPosition? Position,
    EquipmentTemplate Template,
    int DestinationIndex,
    IReadOnlyList<uint> Decorations);

public sealed record EquipmentAddPlan(
    string SourceDigest,
    IReadOnlyList<EquipmentAddPiece> Pieces,
    bool CopySourceDecorations);

public sealed record OwnedTemplateSelection(
    int RecordIndex,
    uint Type,
    uint Id,
    string SourceDigest,
    string StateDigest);

internal sealed record EquipmentCopyRequest(BuildPosition? Position, EquipmentTemplate Template, IReadOnlyList<uint> Decorations);

public sealed partial class DomainEditor
{
    private int[] RecordEvents(int recordIndex)
    {
        if (recordIndex < 0 || recordIndex >= equipment.Count)
            throw new ArgumentOutOfRangeException(nameof(recordIndex));
        int root = equipment[recordIndex].node;
        var events = workspace.Document!.Structure.Events;
        int end = root + 1;
        while (end < events.Count && events[end].Depth > events[root].Depth)
            end++;
        return Enumerable.Range(root, end - root).ToArray();
    }

    private int[] TemplateLeaves(int recordIndex)
    {
        var indices = RecordEvents(recordIndex);
        if (indices.Any(i => workspace.Document!.Structure.Events[i].Tag == 15))
            throw new InvalidDataException("Equipment template has text descendants; fixed-record copying is unsupported.");
        return indices.Where(i => workspace.Document!.Structure.Events[i].DataCoordinate is not null).ToArray();
    }

    private int[] TemplateDecorations(int recordIndex)
    {
        int node = equipment[recordIndex].node;
        int array = Child(node, 4129230907, -1);
        var value = workspace.Document!.Structure.Events[array];
        if (value.ElementTag != 1 || value.Width != 4 || value.Count != 3 || value.Mode != 0)
            throw new InvalidDataException("Template decoration array metadata is not admitted.");
        return PrimitiveArray(node, 4129230907, 3, 1, 4);
    }

    private string TemplateStateDigest(int recordIndex)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (int index in TemplateLeaves(recordIndex))
            hash.AppendData(workspace.PrimitiveBytes(index)!);
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    public EquipmentTemplate ReadEquipmentTemplate(int recordIndex)
    {
        if (recordIndex < 0 || recordIndex >= equipment.Count)
            throw new ArgumentOutOfRangeException(nameof(recordIndex));
        var record = equipment[recordIndex];
        if (record.type < 0 || ReadUInt(record.type, true) is < 1 or > 3)
            throw new InvalidOperationException("Choose an original occupied weapon, armor or talisman template.");
        if (ReadUInt(record.type) != ReadUInt(record.type, true) || ReadUInt(record.id) != ReadUInt(record.id, true))
            throw new InvalidOperationException("Original template type/ID changed; reopen or restore the original identity.");
        TemplateLeaves(recordIndex);
        var fields = TemplateDecorations(recordIndex);
        uint[] original = fields.Select(i => ReadUInt(i, true)).ToArray();
        uint[] current = fields.Select(i => ReadUInt(i)).ToArray();
        if (current.Where((id, at) => id != 0 && id != original[at]).Any())
            throw new InvalidOperationException("Template has an unsupported staged decoration; use zero or its original same-position ID.");
        IReadOnlyList<SkillPair>? pairs = null;
        if (ReadUInt(record.type) == 3)
        {
            var paired = ReadPairs(recordIndex).ToArray();
            if (paired.Any(pair => !knownPairs.Contains(pair)))
                throw new InvalidOperationException("Template talisman has an unobserved skill/level pair.");
            pairs = Array.AsReadOnly(paired);
        }
        return new(recordIndex, ReadUInt(record.type), ReadUInt(record.id), workspace.Document!.Digest,
            TemplateStateDigest(recordIndex), Array.AsReadOnly(current), Array.AsReadOnly(original), pairs);
    }

    public IReadOnlyList<EquipmentTemplate> EquipmentTemplates(uint? type = null)
    {
        var choices = new List<EquipmentTemplate>();
        for (int i = 0; i < equipment.Count; i++)
        {
            try
            {
                var template = ReadEquipmentTemplate(i);
                if (type is null || template.Type == type)
                    choices.Add(template);
            }
            catch (Exception e) when (e is InvalidOperationException or InvalidDataException or ArgumentException)
            {
                // A malformed/nonoriginal record is unavailable as a source.
            }
        }
        return choices.AsReadOnly();
    }

    public int EmptyEquipmentCapacity => equipment.Count(p => p.type >= 0 && ReadUInt(p.type) == 0 && ReadUInt(p.id) == 0);

    internal EquipmentTemplate ValidateTemplateBinding(EquipmentTemplate binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (binding.SourceDigest != workspace.Document!.Digest)
            throw new InvalidOperationException("Template source changed; choose an original template in this selected file.");
        var current = ReadEquipmentTemplate(binding.RecordIndex);
        if (binding.Type != current.Type || binding.Id != current.Id || binding.StateDigest != current.StateDigest ||
           !(binding.Decorations ?? []).SequenceEqual(current.Decorations) ||
           !(binding.OriginalDecorations ?? []).SequenceEqual(current.OriginalDecorations) ||
           !(binding.Pairs ?? []).SequenceEqual(current.Pairs ?? []))
            throw new InvalidOperationException("Template changed while the preview was open; choose it again.");
        return current;
    }

    private void RequireCompatibleMetadata(int sourceIndex, int destinationIndex)
    {
        var source = RecordEvents(sourceIndex);
        var target = RecordEvents(destinationIndex);
        if (source.Length != target.Length)
            throw new InvalidDataException("Equipment destination recursive metadata/leaf shape differs from the template.");
        var events = workspace.Document!.Structure.Events;
        int sourceDepth = events[source[0]].Depth, targetDepth = events[target[0]].Depth;
        for (int at = 0; at < source.Length; at++)
        {
            var a = events[source[at]];
            var b = events[target[at]];
            if (a.Depth - sourceDepth != b.Depth - targetDepth || a.Tag != b.Tag || a.Identifier != b.Identifier ||
               a.ClassIdentifier != b.ClassIdentifier || a.Width != b.Width || a.Count != b.Count || a.Mode != b.Mode ||
               a.ImpliedTag != b.ImpliedTag || a.ElementTag != b.ElementTag ||
               (a.DataCoordinate is null) != (b.DataCoordinate is null))
                throw new InvalidDataException("Equipment destination recursive metadata/array prefix differs from the template.");
        }
        TemplateLeaves(destinationIndex);
    }

    private static void RequirePlannedDecorations(EquipmentTemplate template, IReadOnlyList<uint> decorations)
    {
        if (decorations is null || decorations.Count != 3)
            throw new InvalidOperationException("Exactly three planned decoration positions are required.");
        for (int at = 0; at < 3; at++)
        {
            if (decorations[at] != 0 && decorations[at] != template.OriginalDecorations[at])
                throw new InvalidOperationException($"Unsupported planned decoration at position {at + 1}: only zero or the template's original same-position ID is admitted.");
        }
    }

    public EquipmentAddPlan PreviewEquipmentAdd(EquipmentTemplate template, int quantity, bool copyDecorations = false)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Choose a positive equipment quantity.");
        var current = ValidateTemplateBinding(template);
        if (quantity > EmptyEquipmentCapacity)
            throw new InvalidOperationException("Insufficient empty equipment capacity.");
        uint[] decorations = copyDecorations ? current.Decorations.ToArray() : [0, 0, 0];
        var requests = Enumerable.Range(0, quantity).Select(_ => new EquipmentCopyRequest(null, current, decorations)).ToArray();
        return PreviewEquipmentCopies(requests, copyDecorations);
    }

    internal EquipmentAddPlan PreviewEquipmentCopies(IReadOnlyList<EquipmentCopyRequest> requests, bool copySourceDecorations = false)
    {
        if (requests.Count == 0)
            throw new InvalidOperationException("Select at least one equipment piece to add.");
        var empty = Enumerable.Range(0, equipment.Count).Where(i => equipment[i].type >= 0 && ReadUInt(equipment[i].type) == 0 && ReadUInt(equipment[i].id) == 0).ToArray();
        if (requests.Count > empty.Length)
            throw new InvalidOperationException("Insufficient empty equipment capacity for the complete requested set.");
        var pieces = new List<EquipmentAddPiece>();
        for (int at = 0; at < requests.Count; at++)
        {
            var request = requests[at];
            var template = ValidateTemplateBinding(request.Template);
            RequirePlannedDecorations(template, request.Decorations);
            RequireCompatibleMetadata(template.RecordIndex, empty[at]);
            pieces.Add(new(request.Position, template, empty[at], Array.AsReadOnly(request.Decorations.ToArray())));
        }
        return new(workspace.Document!.Digest, pieces.AsReadOnly(), copySourceDecorations);
    }

    public IReadOnlyList<int> ApplyEquipmentAdd(EquipmentAddPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.SourceDigest != workspace.Document!.Digest)
            throw new InvalidOperationException("Equipment add source changed; preview again.");
        if (plan.Pieces is null || plan.Pieces.Count == 0 || plan.Pieces.Count > equipment.Count)
            throw new InvalidOperationException("Equipment add needs a nonempty bounded set.");
        var destinations = new HashSet<int>();
        var edits = new Dictionary<int, byte[]>();
        foreach (var piece in plan.Pieces)
        {
            if (piece is null)
                throw new InvalidOperationException("Equipment add contains an unresolved piece.");
            var template = ValidateTemplateBinding(piece.Template);
            int destination = piece.DestinationIndex;
            if (destination < 0 || destination >= equipment.Count || !destinations.Add(destination))
                throw new InvalidOperationException("Equipment destination is out of range or duplicated.");
            var target = equipment[destination];
            if (target.type < 0 || ReadUInt(target.type) != 0 || ReadUInt(target.id) != 0)
                throw new InvalidOperationException("Equipment destination is occupied or malformed; preview again.");
            RequireCompatibleMetadata(template.RecordIndex, destination);
            RequirePlannedDecorations(template, piece.Decorations);
            var sourceLeaves = TemplateLeaves(template.RecordIndex);
            var targetLeaves = TemplateLeaves(destination);
            for (int at = 0; at < sourceLeaves.Length; at++)
                edits.Add(targetLeaves[at], workspace.PrimitiveBytes(sourceLeaves[at])!);
            var decorationFields = TemplateDecorations(destination);
            for (int at = 0; at < 3; at++)
                edits[decorationFields[at]] = UIntBytes(piece.Decorations[at]);
        }
        // Every source, target, metadata and planned value has passed validation.
        // This one call is the complete operation's single local history group.
        workspace.Apply(edits);
        return Array.AsReadOnly(plan.Pieces.Select(p => p.DestinationIndex).ToArray());
    }
}
