using FreshFoundation;
if(args.Length==1&&args[0]=="--self-test-r11")return R11Checks.Run();
if(args.Length==1&&args[0]=="--self-test-r10-model")return R10Checks.Run(false);
if(args.Length==1&&args[0]=="--self-test-history")return HistoryChecks.Run();
if(args.Length==1&&args[0]=="--self-test-r10")return R10Checks.Run();
if(args.Length==1&&args[0]=="--self-test-r9")return R9Checks.Run();
if(args.Length==1&&args[0]=="--self-test-providers")return ProviderChecks.Run();
if(args.Length==1&&args[0]=="--self-test-catalog")return CatalogChecks.Run();
if(args.Length>0&&args[0].StartsWith("validation-",StringComparison.Ordinal))return GameValidation.Cli(args);
if(args.Length==1&&args[0]=="--self-test-r6")return ValidationChecks.Run();
if(args.Length==1&&args[0]=="--self-test-r5")return WorkspaceChecks.Run();
if(args.Length==1 && args[0]=="--self-test-r4")return R4Checks.Run();
if(args.Length==1 && args[0]=="--self-test-r3")return R3Checks.Run();
return args.Length == 1 && args[0] == "--self-test" ? R2Checks.Run() : RunCli(args);
static int RunCli(string[] args)
{
    var session = new CopySession();
    try
    {
        if(args.Length == 0) { Console.WriteLine("NO_SELECTION. Explicit usage: inspect <file> | export <file> <new-output> <backup-parent> | restore <selected-file> <backup> <sha256> <new-output>"); return 0; }
        if((args[0] == "inspect" && args.Length == 2) || (args[0] == "export" && args.Length == 4) || (args[0] == "restore" && args.Length == 5)) session.Select(args[1]);
        else throw new ArgumentException("Unsupported command or argument count.");
        var doc = session.Selected!;
        Console.WriteLine($"READ_ONLY Integrity=UNVERIFIED GameSemantics=UNVERIFIED GameIdentity=UNVERIFIED Version={doc.Metadata.Version} Flags={doc.Metadata.Flags} EncodedLength={doc.Metadata.EncodedLength} DecodedLength={doc.Metadata.DecodedLength} SHA256={doc.Digest}");
        Console.WriteLine($"{doc.ChecksumStatus} Structure=EXPERIMENTAL Records={doc.Structure.Summary.Records} ValueEvents={doc.Structure.Summary.ValueEvents} MaximumDepth={doc.Structure.Summary.MaximumDepth} ConsumedCoordinate={doc.Structure.Summary.ConsumedCoordinate} NonzeroPaddingBytes={doc.Structure.Summary.NonzeroPaddingBytes} Arrays={doc.Structure.Summary.ArrayModes.Values.Sum()} TextValues={doc.Structure.Summary.StringCount}");
        if(args[0] == "export") { var receipt = session.Export(args[2],args[3]); Console.WriteLine($"EXPORT_SUCCEEDED Backup={receipt.BackupPath} SHA256={receipt.Digest}"); }
        if(args[0] == "restore") { session.Restore(args[2],args[3],args[4]); Console.WriteLine("RESTORE_COPY_SUCCEEDED"); }
        return 0;
    }
    catch(Exception e) { Console.Error.WriteLine($"FAILED {e.Message}"); return 1; }
}
