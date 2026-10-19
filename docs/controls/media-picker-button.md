# MediaPickerButton

[← All Shiny Controls](../../README.md)

A button that adds photos from the gallery and/or camera, compresses/re-encodes each to PNG or JPEG at a chosen quality (with optional max-dimension downscale), caps the count with `MaxPhotos` (added one at a time), and shows the collected photos inline as a tappable carousel (opening the **ImageViewer**, with an optional **Edit** button that reuses the **ImageEditor**) or a compact pinch/zoom overlay. Ships in the base packages (no extra package). MAUI uses the built-in `MediaPicker`; Blazor uses a hidden `<input type="file">` (with `capture` for the camera) and compresses on an offscreen canvas.

<!-- TODO: capture screenshots for media-picker-button -->

```xml
<shiny:MediaPickerButton Photos="{Binding Photos}"
                         AllowGallery="True"
                         AllowCamera="True"
                         AllowPhotoEdit="True"
                         ShowAsCarouselInView="True"
                         MaxPhotos="5"
                         CompressionQuality="85"
                         OutputFormat="Jpeg"
                         PermissionDeniedText="Photo access was denied — enable it in Settings." />
```

| Property | Type | Default | Description |
|---|---|---|---|
| AllowGallery | bool | true | Offer "choose from gallery" |
| AllowCamera | bool | true | Offer "take photo" (chooser shown when both are enabled; on Blazor on iPhone/iPad the browser's own library/camera/files sheet is used instead) |
| AllowPhotoEdit | bool | false | Show an Edit button that opens the ImageEditor |
| PermissionDeniedText | string | "Permission denied…" | Shown when camera/gallery access is denied |
| NoImagesTemplate | DataTemplate? | null | Shown when there are no photos yet |
| ShowAsCarouselInView | bool | true | Inline carousel (true) vs compact preview + pinch/zoom overlay (false) |
| MaxPhotos | int | 1 | Maximum photos (added one at a time) |
| CompressionQuality | int | 92 | Encoder quality percentage (1–100) |
| MaxImageDimension | int | 0 | If > 0, longest edge is downscaled to this many pixels |
| OutputFormat | ImageExportFormat | Jpeg | Output encoding (Png or Jpeg) |
| Photos | IList\<MediaPickerItem\> | empty | Collected photos (TwoWay). Hand in photos the screen already has; set `CanRemove = false` on any that must stay |
| ItemSpacing | double | 0 (MAUI) / 12 (Blazor) | Space between thumbnails in the inline carousel |
| AddButtonTemplate | DataTemplate? / RenderFragment? | null | Replaces the dashed "Add Photo" trigger |

## Photos that can't be removed

`MediaPickerItem.CanRemove` (default `true`) controls the ✕ on a thumbnail. Set it to `false` for a photo the screen loaded itself — one already saved — so the user can add to the set but not take that one out. It survives an edit. Removal from code (`Photos.Remove`, Blazor `ClearAsync`, a successful upload) is not blocked.

```csharp
// MAUI — MediaPickerItem is a record, so CanRemove is init-only
Photos.Add(new MediaPickerItem(bytes, 900, 1200, "image/jpeg") { CanRemove = false });
```

```razor
@* Blazor — hand in saved photos by URL; the picker shows them as it shows its own *@
<MediaPickerButton @bind-Photos="photos" MaxPhotos="5" />

@code {
    IReadOnlyList<MediaPickerItem> photos =
    [
        new() { DataUri = "/photos/41.jpg", ContentType = "image/jpeg", CanRemove = false }
    ];
}
```

On Blazor, `Photos` is now read as well as written: a list the screen passes in replaces what the picker holds. A photo handed in by URL has no bytes in the browser, so `UploadAllAsync` skips it (it is not waiting to be sent), it can't be opened in the editor unless its `Data` is set, and `ReadAllBytesAsync` throws for it.

## Custom add button

`AddButtonTemplate` replaces the dashed trigger. The template is the whole tap target, and it still hides at `MaxPhotos`.

```xml
<shiny:MediaPickerButton Photos="{Binding Photos}" MaxPhotos="3" ItemSpacing="12">
    <shiny:MediaPickerButton.AddButtonTemplate>
        <DataTemplate>
            <Border Padding="16" StrokeShape="RoundRectangle 16">
                <Label Text="📷  Add up to 3 photos" HorizontalOptions="Center" />
            </Border>
        </DataTemplate>
    </shiny:MediaPickerButton.AddButtonTemplate>
</shiny:MediaPickerButton>
```

```razor
<MediaPickerButton @bind-Photos="photos" MaxPhotos="3" ItemSpacing="16" AddButtonText="Add up to 3 photos">
    <AddButtonTemplate>
        <div class="my-add-tile">📷 Add up to 3 photos</div>
    </AddButtonTemplate>
</MediaPickerButton>
```

Keep the template to visuals. On MAUI a `Button` inside it takes the tap for itself and nothing opens; on Blazor it is rendered inside a `<button>`, so a nested button or link is invalid markup. On Blazor, `AddButtonText` stays as the button's accessible name.

**Events:** `PhotoAdded`, `PhotoRemoved`, `PhotosChanged` (+ `PhotosChangedCommand`), `PermissionDenied`

On Blazor the equivalent `Shiny.Blazor.Controls.MediaPickerButton` mirrors these as `[Parameter]`s (`OutputFormat` is `"jpeg"`/`"png"`), with `@bind-Photos` over `MediaPickerItem` (each exposing a `DataUri` for `<img src>`).

## Where the bytes live (Blazor)

A picked photo stays in the browser. What crosses into .NET is a descriptor — id, an object URL for `<img src>`, dimensions, content type and size — and the bytes themselves move only when something asks for them, always as binary:

| You want | What happens |
|---|---|
| `MediaPickerItem.Data` | Read in chunks over a JS stream when the photo is picked (the default) |
| `await item.OpenReadStreamAsync()` / `ReadAllBytesAsync()` | The same stream, on demand |
| `UploadUrl` set | The browser posts the photo as multipart form data itself; the bytes never enter .NET |

Nothing is base64-encoded. That matters most on **Blazor Server**, where a photo carried over the circuit is one SignalR message: bigger than the default 32KB `MaximumReceiveMessageSize`, so the server closed the connection and the upload failed.

### Uploading straight from the browser

```razor
<MediaPickerButton @ref="picker"
                   @bind-Photos="photos"
                   MaxPhotos="5"
                   MaxImageDimension="1600"
                   CompressionQuality="85"
                   UploadProgress="OnProgress" />

@code {
    MediaPickerButton? picker;
    IReadOnlyList<MediaPickerItem> photos = [];
    int percent;

    // Save the record FIRST, so the photos have something that exists to be addressed to.
    async Task Save()
    {
        var id = await SaveTheThing();
        var results = await picker!.UploadAllAsync(new MediaPickerUpload($"/photos/{id}")
        {
            Headers = new Dictionary<string, string> { ["X-Upload-Ticket"] = ticket }
        });

        foreach (var failed in results.Where(r => !r.Success))
            ...   // the photo is still picked, so offering "try again" costs the user nothing
    }

    void OnProgress(MediaPickerUploadProgress p) { percent = p.Percent ?? 0; StateHasChanged(); }
}
```

Set `AutoUpload="true"` to send each photo the moment it is picked, when the address is known up front. `Uploaded` reports each result, and the server's answer comes back verbatim in `MediaPickerUploadResult.Body` for the caller to read.

### Blazor-only properties

| Property | Type | Default | Description |
|---|---|---|---|
| UploadUrl | MediaPickerUpload? | null | Where the browser posts each photo (`Url`, `FieldName`, `FileName`, `Method`, `Headers`, `WithCredentials`) |
| AutoUpload | bool | false | Upload as soon as a photo is picked, instead of waiting for `UploadAllAsync` |
| LoadBytes | bool? | null | Fill `MediaPickerItem.Data` when picked. Defaults to true, or false when `UploadUrl` is set |
| MaxReadSize | long | 32MB | Refuses to read a photo larger than this into .NET |

**Methods:** `UploadAllAsync(upload?, keep?, ct)`, `UploadAsync(item, upload?, keep?, ct)`, `ClearAsync()`.
**Events:** `Uploaded` (`MediaPickerUploadResult`), `UploadProgress` (`MediaPickerUploadProgress`).

A successful upload takes the photo out of the picker unless `keep: true` — which is for sending the same photos to more than one place.
