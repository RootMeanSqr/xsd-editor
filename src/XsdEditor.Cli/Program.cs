// Headless harness for the schema core. CI drives round-trip and timing runs through
// this so that neither needs a display.

if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
{
    Console.WriteLine("""
        xsdedit — headless harness for the xsd-editor schema core

        Usage:
          xsdedit format    [file]    reformat to the configured style; stdin if no file
          xsdedit roundtrip <file>    parse and re-serialise, reporting any diff
          xsdedit time      <file>    report parse, serialise and validation timings

        Corpus fixtures are located through XSDEDITOR_CORPUS, per
        docs/decisions/0004-build-and-security-tooling.md.
        """);
    return 0;
}

// `roundtrip` lands with the syntax layer. `format` and `time` follow with the formatter
// and the validation path, later in Phase 1.
if (args[0] == "roundtrip")
{
    return XsdEditor.Cli.RoundTripCommand.Run(args[1..]);
}

Console.Error.WriteLine($"xsdedit: '{args[0]}' is not implemented yet (Phase 1).");
return 2;
