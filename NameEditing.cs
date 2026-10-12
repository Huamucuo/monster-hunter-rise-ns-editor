using System.Text;
namespace FreshFoundation;

public sealed record NameEditAvailability(bool CanRename, string Detail, string? OriginalName, string? CurrentName);

public sealed partial class DomainEditor
{
    private (int primary, int equipmentOwner) OwnerNameFields()
    {
        int owner = Child(RootWith(3575327903), 3575327903, 17);
        int primary = Child(owner, 45809936, 15);
        int equipmentOwner = Child(Child(owner, 257730785, 17), 264986686, 15);
        if (!workspace.ValueBytes(primary, true).AsSpan().SequenceEqual(workspace.ValueBytes(equipmentOwner, true)))
            throw new InvalidDataException("Required original owner name mirrors differ.");
        if (!workspace.ValueBytes(primary).AsSpan().SequenceEqual(workspace.ValueBytes(equipmentOwner)))
            throw new InvalidDataException("Required staged owner name mirrors differ.");
        return (primary, equipmentOwner);
    }

    internal byte[] OwnerNameBytes(bool original = false)
    {
        var fields = OwnerNameFields();
        return workspace.ValueBytes(fields.primary, original);
    }

    public NameEditAvailability NameEditStatus()
    {
        try
        {
            var fields = OwnerNameFields();
            return new(true, "Two admitted local owner name routes are available. Game name rules remain unverified.",
                workspace.TextAt(fields.primary, true), workspace.TextAt(fields.primary));
        }
        catch (InvalidDataException e)
        {
            return new(false, "Required owner name routes are unavailable: " + e.Message, null, null);
        }
    }

    internal static void ValidateName(string value)
    {
        if (value is null || value.Length is < 1 or > 16 || string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Name must contain 1–16 UTF-16 units and cannot be whitespace-only. This is an application policy, not a verified game maximum.");
        if (value.Any(c => c == '\0' || char.IsControl(c)))
            throw new ArgumentException("Name cannot contain NUL or control characters.");
        try
        {
            new UnicodeEncoding(false, false, true).GetByteCount(value);
        }
        catch (EncoderFallbackException e) { throw new ArgumentException("Name cannot contain unpaired UTF-16 surrogates.", e); }
    }

    public void SetName(string value)
    {
        ValidateName(value);
        var fields = OwnerNameFields();
        byte[] bytes = Encoding.Unicode.GetBytes(value);
        workspace.Apply(new Dictionary<int, byte[]> { { fields.primary, bytes }, { fields.equipmentOwner, bytes } });
    }

    internal bool IsOwnerNameField(int index)
    {
        try
        {
            var fields = OwnerNameFields();
            return index == fields.primary || index == fields.equipmentOwner;
        }
        catch (InvalidDataException) { return false; }
    }
}

public sealed partial class DirectoryEditor
{
    private int cachedName = -1;
    private bool directoryRenameContext;
    private string? renameLinkIssue;
    public string NameMirrorStatus { get; private set; } = "Single-file use: only the two local owner fields can be renamed; system preview is not linked.";
    public string? CachedName => System is not null && cachedName >= 0 ? System.TextAt(cachedName) : null;
    public bool CanRename => Domain?.NameEditStatus().CanRename == true && (!directoryRenameContext || renameLinkIssue is null && System is not null && cachedName >= 0);

    private void ResetNameLink()
    {
        cachedName = -1;
        directoryRenameContext = false;
        renameLinkIssue = null;
        NameMirrorStatus = "Single-file use: only the two local owner fields can be renamed; system preview is not linked.";
    }

    private void InitializeNameLink()
    {
        ResetNameLink();
        if (directory is null || SlotNumber is null || !string.Equals(Path.GetDirectoryName(Slot.SourcePath), directory, StringComparison.OrdinalIgnoreCase))
            return;
        directoryRenameContext = true;
        try
        {
            string path = Path.Combine(directory, "data00-1.bin");
            if (!File.Exists(path))
                throw new InvalidDataException("Corresponding system file is missing; explicitly open the slot as a single file to rename only local owner fields.");
            var system = System ?? new EditorWorkspace();
            if (system.Document is null)
                system.Open(path);
            var arrays = system.ChildIndices().SelectMany(root => system.ChildIndices(root))
                .Where(i => system.Document!.Structure.Events[i].Identifier == 28334441).ToArray();
            if (arrays.Length != 1 || system.Document!.Structure.Events[arrays[0]].Tag != -1)
                throw new InvalidDataException("System character array is absent, malformed or ambiguous.");
            var array = system.Document.Structure.Events[arrays[0]];
            var hunters = system.ChildIndices(arrays[0]);
            if (array.ElementTag != 17 || array.Width != 8 || array.Mode != 1 || array.Count != 3 || hunters.Count != 3 || hunters.Any(i => system.Document.Structure.Events[i].Tag != 17))
                throw new InvalidDataException("System character array must have exactly three admitted object entries.");
            var helper = new DomainEditor(system);
            int target = helper.Child(hunters[SlotNumber.Value - 1], 4154407670, 15);
            var owner = Domain!.NameEditStatus();
            if (!owner.CanRename)
                throw new InvalidDataException(owner.Detail);
            if (!system.ValueBytes(target, true).AsSpan().SequenceEqual(Domain.OwnerNameBytes(true)))
                throw new InvalidDataException("Original system/owner name correlation differs.");
            System = system;
            cachedName = target;
            NameMirrorStatus = "Directory context: both local owner names and the corresponding system name are staged as one action. Native game acceptance is unverified.";
        }
        catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            renameLinkIssue = "System name link cannot be validated: " + e.Message;
            NameMirrorStatus = "Rename unavailable in directory context. " + renameLinkIssue;
        }
    }

    public void SetName(string value)
    {
        if (Domain is null)
            throw new InvalidOperationException("Select a slot before editing its name.");
        DomainEditor.ValidateName(value);
        var owner = Domain.NameEditStatus();
        if (!owner.CanRename)
            throw new InvalidOperationException(owner.Detail);
        if (directoryRenameContext)
        {
            if (renameLinkIssue is not null || System is null || cachedName < 0)
                throw new InvalidOperationException(renameLinkIssue ?? "System name link is unavailable.");
            var target = System.Document!.Structure.Events[cachedName];
            if (target.Tag != 15 ||
                !System.ValueBytes(cachedName, true).AsSpan().SequenceEqual(Domain.OwnerNameBytes(true)) ||
                !System.ValueBytes(cachedName).AsSpan().SequenceEqual(Domain.OwnerNameBytes()))
                throw new InvalidOperationException("System/owner name link changed; reopen the directory before renaming.");
        }
        Track(() =>
        {
            Domain.SetName(value);
            if (directoryRenameContext)
                System!.ApplyText(cachedName, value);
        });
    }
}
