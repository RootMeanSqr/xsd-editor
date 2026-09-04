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

// The commands land with the parser in Phase 1; the harness exists from Phase 0 so that
// CI wiring is in place before there is anything to measure.
Console.Error.WriteLine($"xsdedit: '{args[0]}' is not implemented yet (Phase 1).");
return 2;
