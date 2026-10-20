using System.Text;
using Shiny.Controls.Office.Document;
using Shiny.Controls.Office.Shell;
using Shiny.Controls.Office.Skia;
using Shouldly;
using Xunit;

namespace Shiny.Controls.Office.Tests;

/// <summary>
/// The document store contract the editors' File backstage runs on: what a Save overwrites, what a
/// Save As creates, what the recent list remembers. The editors on both hosts only route to these.
/// </summary>
public sealed class OfficeDocumentStoreTests : IDisposable
{
    readonly string folder = Path.Combine(Path.GetTempPath(), "shiny-office-store-" + Guid.NewGuid().ToString("N"));
    readonly SteppingClock clock = new(new DateTimeOffset(2026, 10, 19, 9, 0, 0, TimeSpan.Zero));

    public void Dispose()
    {
        if (Directory.Exists(this.folder))
            Directory.Delete(this.folder, recursive: true);
    }

    FileSystemOfficeDocumentStore Store() => new(this.folder, this.clock);

    static Func<Stream, CancellationToken, Task> Text(string text)
        => (s, ct) => s.WriteAsync(Encoding.UTF8.GetBytes(text), ct).AsTask();

    static async Task<string> ReadAsync(IOfficeDocumentStore store, OfficeDocumentRef file)
    {
        await using var stream = await store.OpenReadAsync(file);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    // ---- session: what Save means --------------------------------------------------------------

    [Fact]
    public async Task TheFirstSaveCreatesAFileAndTheNextOneOverwritesIt()
    {
        var store = this.Store();
        var session = new OfficeDocumentSession(OfficeApp.Word);

        var first = await session.SaveAsync(store, OfficeFileFormats.Docx, OfficeFileAction.Save, "Report", Text("one"));
        var second = await session.SaveAsync(store, OfficeFileFormats.Docx, OfficeFileAction.Save, "Report", Text("two"));

        first.ShouldNotBeNull();
        first.Name.ShouldBe("Report.docx");
        second!.Id.ShouldBe(first.Id);
        session.Current.ShouldBe(second);
        (await ReadAsync(store, second)).ShouldBe("two");
        Directory.GetFiles(this.folder, "*.docx").Length.ShouldBe(1);
    }

    [Fact]
    public async Task SaveAsNeverOverwritesAndBecomesTheOpenFile()
    {
        var store = this.Store();
        var session = new OfficeDocumentSession(OfficeApp.Word);

        var original = await session.SaveAsync(store, OfficeFileFormats.Docx, OfficeFileAction.Save, "Report", Text("one"));
        var copy = await session.SaveAsync(store, OfficeFileFormats.Docx, OfficeFileAction.SaveAs, "Report", Text("two"));

        copy!.Name.ShouldBe("Report (2).docx");
        session.Current.ShouldBe(copy);
        (await ReadAsync(store, original!)).ShouldBe("one");
    }

    [Fact]
    public async Task AnExportIsANewFileAndLeavesTheOpenFileAlone()
    {
        var store = this.Store();
        var session = new OfficeDocumentSession(OfficeApp.Word);
        var saved = await session.SaveAsync(store, OfficeFileFormats.Docx, OfficeFileAction.Save, "Report", Text("doc"));

        var pdf = await session.SaveAsync(store, OfficeFileFormats.Pdf, OfficeFileAction.Export, "Report", Text("pdf"));

        pdf!.Name.ShouldBe("Report.pdf");
        session.Current.ShouldBe(saved);
    }

    [Fact]
    public async Task SaveAsInAForeignFormatDoesNotRetargetTheNextSave()
    {
        var store = this.Store();
        var session = new OfficeDocumentSession(OfficeApp.Excel);
        var saved = await session.SaveAsync(store, OfficeFileFormats.Xlsx, OfficeFileAction.Save, "Book1", Text("xlsx"));

        await session.SaveAsync(store, OfficeFileFormats.Csv, OfficeFileAction.SaveAs, "Book1", Text("csv"));
        var again = await session.SaveAsync(store, OfficeFileFormats.Xlsx, OfficeFileAction.Save, "Book1", Text("xlsx 2"));

        again!.Id.ShouldBe(saved!.Id);
    }

    [Fact]
    public async Task ACancelledSaveLeavesTheOpenFileAlone()
    {
        var session = new OfficeDocumentSession(OfficeApp.Word) { Current = new OfficeDocumentRef("x", "x.docx") };

        var written = await session.SaveAsync(new CancellingStore(), OfficeFileFormats.Docx, OfficeFileAction.SaveAs, "x", Text("x"));

        written.ShouldBeNull();
        session.Current!.Id.ShouldBe("x");
    }

    [Fact]
    public async Task OpeningMovesTheOpenFileOnlyOnceTheLoadSucceeded()
    {
        var store = this.Store();
        var session = new OfficeDocumentSession(OfficeApp.Word);
        var good = await session.SaveAsync(store, OfficeFileFormats.Docx, OfficeFileAction.SaveAs, "Good", Text("good"));
        var bad = await session.SaveAsync(store, OfficeFileFormats.Docx, OfficeFileAction.SaveAs, "Bad", Text("bad"));
        session.Current = good;

        await Should.ThrowAsync<InvalidDataException>(() => session.OpenAsync<string>(store, bad, (_, _) => throw new InvalidDataException()));
        session.Current.ShouldBe(good);

        var opened = await session.OpenAsync(store, bad, async (s, ct) => await new StreamReader(s).ReadToEndAsync(ct));
        opened!.Value.Document.ShouldBe("bad");
        session.Current.ShouldBe(bad);
    }

    [Fact]
    public async Task NothingToPickWithIsACancelledOpen()
    {
        var session = new OfficeDocumentSession(OfficeApp.Word);

        var opened = await session.OpenAsync(this.Store(), null, (_, _) => Task.FromResult(1));

        opened.ShouldBeNull();
        session.Current.ShouldBeNull();
    }

    // ---- file system store ---------------------------------------------------------------------

    [Fact]
    public async Task AFileNameThatIsAPathStaysInTheFolder()
    {
        var store = this.Store();
        var request = new OfficeSaveRequest(OfficeApp.Word, OfficeFileFormats.Docx, "../../Q1/Q2.docx", OfficeFileAction.SaveAs, Text("x"));

        var written = await store.SaveAsync(null, request);

        Path.GetDirectoryName(written!.Id).ShouldBe(store.Folder);
        written.Name.ShouldBe("-..-Q1-Q2.docx");
    }

    [Fact]
    public async Task AFailedWriteLeavesTheOldFileAndNoTemporaryOne()
    {
        var store = this.Store();
        var session = new OfficeDocumentSession(OfficeApp.Word);
        var saved = await session.SaveAsync(store, OfficeFileFormats.Docx, OfficeFileAction.Save, "Report", Text("good"));

        await Should.ThrowAsync<IOException>(() => session.SaveAsync(store, OfficeFileFormats.Docx, OfficeFileAction.Save, "Report",
            async (s, ct) =>
            {
                await s.WriteAsync("half"u8.ToArray(), ct);
                throw new IOException("disk full");
            }));

        (await ReadAsync(store, saved!)).ShouldBe("good");
        Directory.GetFiles(this.folder).Where(x => !x.EndsWith(FileSystemOfficeDocumentStore.IndexFileName)).Count().ShouldBe(1);
    }

    [Fact]
    public async Task TheRecentListIsPerAppNewestFirstAndOnlyTheAppsOwnFiles()
    {
        var store = this.Store();
        var words = new OfficeDocumentSession(OfficeApp.Word);
        var sheets = new OfficeDocumentSession(OfficeApp.Excel);

        await words.SaveAsync(store, OfficeFileFormats.Docx, OfficeFileAction.SaveAs, "Older", Text("a"));
        this.clock.Step();
        await sheets.SaveAsync(store, OfficeFileFormats.Xlsx, OfficeFileAction.SaveAs, "Budget", Text("b"));
        this.clock.Step();
        await words.SaveAsync(store, OfficeFileFormats.Docx, OfficeFileAction.SaveAs, "Newer", Text("c"));
        await words.SaveAsync(store, OfficeFileFormats.Pdf, OfficeFileAction.Export, "Newer", Text("pdf"));

        var recent = await store.GetRecentAsync(OfficeApp.Word);

        recent.Select(x => x.Name).ShouldBe(["Newer.docx", "Older.docx"]);
        recent[0].Document.ShouldNotBeNull();
        recent[0].Location.ShouldBe(store.Folder);
        (await store.GetRecentAsync(OfficeApp.Excel)).Single().Name.ShouldBe("Budget.xlsx");
    }

    [Fact]
    public async Task OpeningAFileMovesItToTheTopAndADeletedOneDropsOut()
    {
        var store = this.Store();
        var session = new OfficeDocumentSession(OfficeApp.Word);
        var a = await session.SaveAsync(store, OfficeFileFormats.Docx, OfficeFileAction.SaveAs, "A", Text("a"));
        this.clock.Step();
        var b = await session.SaveAsync(store, OfficeFileFormats.Docx, OfficeFileAction.SaveAs, "B", Text("b"));
        this.clock.Step();

        (await store.OpenReadAsync(a!)).Dispose();
        (await store.GetRecentAsync(OfficeApp.Word)).Select(x => x.Name).ShouldBe(["A.docx", "B.docx"]);

        File.Delete(b!.Id);
        (await store.GetRecentAsync(OfficeApp.Word)).Select(x => x.Name).ShouldBe(["A.docx"]);
    }

    [Fact]
    public async Task TheRecentListIsCappedAndACorruptIndexStartsItAgain()
    {
        var store = new FileSystemOfficeDocumentStore(this.folder, this.clock) { MaxRecentFiles = 2 };
        var session = new OfficeDocumentSession(OfficeApp.Word);

        foreach (var name in new[] { "A", "B", "C" })
        {
            await session.SaveAsync(store, OfficeFileFormats.Docx, OfficeFileAction.SaveAs, name, Text(name));
            this.clock.Step();
        }

        (await store.GetRecentAsync(OfficeApp.Word)).Select(x => x.Name).ShouldBe(["C.docx", "B.docx"]);

        await File.WriteAllTextAsync(Path.Combine(this.folder, FileSystemOfficeDocumentStore.IndexFileName), "{ not json");
        (await store.GetRecentAsync(OfficeApp.Word)).ShouldBeEmpty();
    }

    // ---- what the editors write ----------------------------------------------------------------

    [Fact]
    public async Task ADocumentRoundTripsThroughTheStoreAndSavingMarksItClean()
    {
        var store = this.Store();
        var session = new OfficeDocumentSession(OfficeApp.Word);

        using var document = await WordTemplates.OpenAsync(WordTemplates.All.First(x => x.IsBlank));
        document.MarkDirty();

        var written = await session.SaveAsync(store, OfficeFileFormats.Docx, OfficeFileAction.Save, "Blank",
            (s, ct) => DocumentExport.WriteAsync(document, OfficeFileFormats.Docx, s, cancellationToken: ct));
        document.MarkClean();

        document.IsDirty.ShouldBeFalse();
        var reopened = await session.OpenAsync(store, written, (s, ct) => WordDocument.OpenAsync(s, editable: true, cancellationToken: ct));
        using var copy = reopened!.Value.Document;
        copy.PlainText.ShouldBe(document.PlainText);
    }

    [Theory]
    [InlineData("docx")]
    [InlineData("pdf")]
    [InlineData("txt")]
    [InlineData("html")]
    public async Task EveryWordFormatTheBackstageOffersHasAWriter(string id)
    {
        var format = OfficeFileFormats.SaveAsFor(OfficeApp.Word).Concat(OfficeFileFormats.ExportFor(OfficeApp.Word)).First(x => x.Id == id);
        // Not the blank one: its plain text is rightly empty.
        using var document = await WordTemplates.OpenAsync(WordTemplates.All.First(x => !x.IsBlank));

        DocumentExport.CanWrite(format).ShouldBeTrue();
        (await DocumentExport.ToBytesAsync(document, format, new DocumentPdfOptions { Title = "T" })).Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void AnUnknownFormatIsRefused()
        => DocumentExport.CanWrite(OfficeFileFormats.Csv).ShouldBeFalse();

    [Fact]
    public async Task ACancelledOpenDoesNotLeaveTheFileOpen()
    {
        var store = this.Store();
        var session = new OfficeDocumentSession(OfficeApp.Word);
        var saved = await session.SaveAsync(store, OfficeFileFormats.Docx, OfficeFileAction.Save, "Report", Text("doc"));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        // The recent-list update after the file is opened is what sees the cancellation.
        await Should.ThrowAsync<OperationCanceledException>(() => store.OpenReadAsync(saved!, cancelled.Token));

        // A handle left behind would hold its share lock until a finalizer ran.
        Should.NotThrow(() => new FileStream(saved!.Id, FileMode.Open, FileAccess.ReadWrite, FileShare.None).Dispose());
    }

    [Fact]
    public void TheDisplayNameDropsTheExtension()
    {
        new OfficeDocumentRef("1", "Budget 2026.xlsx").DisplayName.ShouldBe("Budget 2026");
        new OfficeDocumentRef("1", "Notes").DisplayName.ShouldBe("Notes");
    }


    sealed class SteppingClock(DateTimeOffset start) : TimeProvider
    {
        DateTimeOffset now = start;

        public void Step() => this.now = this.now.AddMinutes(1);

        public override DateTimeOffset GetUtcNow() => this.now;
    }

    sealed class CancellingStore : IOfficeDocumentStore
    {
        public Task<OfficeDocumentRef?> PickOpenAsync(OfficeApp app, CancellationToken cancellationToken = default) => Task.FromResult<OfficeDocumentRef?>(null);
        public Task<Stream> OpenReadAsync(OfficeDocumentRef document, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<OfficeDocumentRef?> SaveAsync(OfficeDocumentRef? target, OfficeSaveRequest request, CancellationToken cancellationToken = default) => Task.FromResult<OfficeDocumentRef?>(null);
        public Task<IReadOnlyList<OfficeRecentFile>> GetRecentAsync(OfficeApp app, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<OfficeRecentFile>>([]);
    }
}
