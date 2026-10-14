# PDF/UA Accessibility for Ticket Exports

## Scope

The ticket board exports the current page or all tickets in the active permitted view. Both paths call `ITicketPdfService.Generate`, so accessibility changes are shared by current-page and all-ticket exports.

The implementation targets **PDF/UA-1** and includes:

- A tagged PDF document created through iText's `PdfUADocument`.
- A declared document language (`en-US`).
- A meaningful document title in metadata, with the PDF viewer preference set to display the title.
- An H1 role for the visible report title.
- Table headers with semantic `TH` roles and cells rendered as tagged table content.
- Embedded fonts using Identity-H encoding, with Windows and Linux font-path discovery.
- Readable table typography, adequate foreground/background contrast, and alternating row backgrounds.
- A test that checks generated PDF structure, title, language, title-display preference, and page count.

The Linux Docker image installs `fonts-dejavu-core` so the same export path has an embeddable font available in the container. Windows development resolves Arial first, with Segoe UI and Calibri as fallbacks.

## Validation after building

A successful .NET build and automated test are useful regression checks, but do not establish PDF/UA conformance by themselves. Validate a newly generated file in PAC 2026.x:

1. Build and test the feature branch.
2. Run Kestrel Helpdesk and export **Current page** with at least one ticket.
3. Export **All tickets** with at least one ticket.
4. Open both generated PDFs in PAC and select **PDF/UA-1**.
5. Confirm the PDF has no automated failures. If PAC still reports failures, capture the detailed result/checkpoint view and use those exact failures to continue remediation.
6. Inspect the tag tree and reading order manually. Confirm the document title is first, the summary follows it, and the ticket data is represented as a table with meaningful header relationships.
7. Check the document using a screen reader or an accessibility reviewer before production handover.

## Important limitation

The supplied PAC report is an aggregate one-page summary. It reports that the previous export was not PDF/UA-1 compliant, with failures in fonts, content, natural language, logical structure, metadata, and document settings, but it does not include individual object-level diagnostics. The source changes address these categories structurally; the result must still be confirmed by running PAC against PDFs generated from this branch. Do not describe the export as certified or verified compliant until that validation passes.
