# Adding your design

## The easy way: from the plugin

Open the Lock Screen Explorer, go to **Community** and press **S**. Pick one of
your own designs, fill in the description, tags and your GitHub username, and
press Enter. It renders the screenshot for you, lays the folder out, runs the
same check this repository runs, and — if `gh` is signed in — opens the pull
request. If it is not, it leaves you a finished folder and tells you what to
run.

Everything below is that, by hand.


A design is one folder. Open a pull request that adds it and nothing else.

```
Designs/<your-id>/
  design.json
  YourDesign.qml
  preview.png     (or .jpg)
```

`<your-id>` is lowercase words joined by hyphens — `sunset`, `mono-rail`. It is
how the design is identified forever, so pick something you will not want to
change.

## design.json

```json
{
  "name": "Sunset",
  "author": "your-github-username",
  "description": "A warm gradient with a thin clock over it.",
  "tags": ["minimal", "clock"]
}
```

- **name** — 2 to 40 characters.
- **description** — 10 to 200 characters, one sentence.
- **tags** — one to three of: `minimal`, `cards`, `clock`, `type`, `dark`,
  `fun`, `reactive`.

## The QML

Start from
[`extras/lock-designs/MyDesign.qml`](https://github.com/SirJul1337/omarchy-lock-explorer/blob/main/extras/lock-designs/MyDesign.qml)
in the plugin, or from any built-in design. The root item must be `DesignBase`,
and the file must keep the import that pulls in the shared parts:

```qml
import QtQuick
import qs.Commons
import "../plugins/io.github.sirjul1337.lock-explorer/designs"

DesignBase {
  id: lock
  inputItem: field.input
  …
}
```

Take colours and fonts from the theme (`Color.lock.*`, `Style.font.*`) rather
than hard-coding them — a design that only looks right on your theme will look
wrong on everyone else's.

### What a design may not do

A lock screen draws, reads the clock and the user name, and takes a password.
Anything past that is refused, because someone is typing their password into
your file:

- Only these imports: `QtQuick`, `QtQuick.*`, `qs.Commons`, and the designs
  import above. In particular **not** `Quickshell.Io` — that is where
  `Process` and `FileView` live.
- No `Process`, `XMLHttpRequest`, sockets, `FileView`, `XmlListModel`, `eval`,
  `Qt.openUrlExternally`, `Qt.createQmlObject`, `Qt.include`, or a `Loader`
  with a runtime `source`.
- No network URLs and no absolute `file://` paths.
- 64 KB maximum.

**No video designs yet.** A clip needs a different kind of review and is not
accepted here for now.

## The preview

A real screenshot of your design, 16:9, under 1 MB. On an Omarchy machine with
the plugin installed:

```sh
cp YourDesign.qml ~/.config/omarchy/lock-designs/
omarchy-shell lock rescanDesigns
omarchy-shell lock previewDesign my-yourdesign
grim -o "$(hyprctl monitors -j | jq -r '.[0].name')" preview.png
omarchy-shell lock hidePreview
```

Shrink it if it is large: `ffmpeg -i preview.png -vf scale=1280:-1 -q:v 5 preview.jpg`

## One thing the plugin changes

Keep the import exactly as the template has it. When the plugin installs your
design it lands in `~/.config/omarchy/lock-designs/community/<your-id>/`, one
level deeper than the template assumes, so the installer adds the two extra
`../` steps that line needs. That happens after your file has been checksummed
and re-scanned, and it is the only edit ever made to it — so write the import
the normal way and let the installer deal with the depth.

## Before you open the PR

```sh
python scripts/scan.py Designs/<your-id>
```

It prints every problem with a reason. The same check runs on your pull
request, so getting it clean locally means CI will pass. Do not edit
`index.json` — it is generated on merge.

Then a maintainer reads your QML and merges. Once merged it appears on the site
and in the plugin's Community list within a few minutes.

## Changing or removing a design

A change is a normal pull request to your folder. To have a design taken down,
open an issue or a PR deleting the folder — it disappears from the registry on
the next build, and the plugin drops it.

## Licence

By opening a pull request you agree that your design is published here under
this repository's MIT licence.
