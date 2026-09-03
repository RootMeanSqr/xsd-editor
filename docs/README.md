# Documentation

| File                                 | Purpose                                       |
| ------------------------------------ | --------------------------------------------- |
| [`requirements.md`](requirements.md) | The specification — what we are building      |
| [`implementation-plan.md`](implementation-plan.md) | The phased route from here to a shipping editor |
| [`development.md`](development.md)   | Setting up a local development environment    |
| `decisions/`                         | One short record per hard-to-reverse decision |
| `measurements/`                      | Recorded timings and spike results            |

A decision record is a single markdown file named `NNNN-short-title.md` containing the context, the
decision, and its consequences.

| Record                                                 | Decision                            |
| ------------------------------------------------------ | ----------------------------------- |
| [`0001-licensing.md`](decisions/0001-licensing.md)      | Apache-2.0, with paid maintenance   |
| [`0002-technology-stack.md`](decisions/0002-technology-stack.md) | .NET 10 and Avalonia       |
| [`0003-text-view-editor-control.md`](decisions/0003-text-view-editor-control.md) | AvaloniaEdit, pending a spike (**proposed**) |
| [`0004-build-and-security-tooling.md`](decisions/0004-build-and-security-tooling.md) | Corpus access, static analysis, lock files |
