# ClearText interface study — native Windows revision

The user rejected the Tkinter prototype as dated. The current 0.3.0 app is
C# / WPF, with a custom styled document canvas, navigation rail, rounded tabs,
an inline search bar, and a tutorial rail. No Python runtime is involved.
The earlier reference research below informed the hierarchy; it is not a claim
that the old prototype's widgets or layout remain in the released app.

Date: 2026-10-15. Used the user's configured **ui-design MCP** with
`search(type="screens", query="text editor", platform="web", limit=4)` and
`get_image` for the first Descript result. These are web references; Windows
desktop usability still needs its own verification. No third-party screenshots
are redistributed in the source or release archives.

## References and findings

1. [Descript text workspace, capture 1](https://images.refero.design/screenshots/descript.com/desktop/b4eefd4c-e222-44d9-8168-5cdbb4371b96/0.jpg):
   visually inspected at 1200px through get_image. A large document canvas sits
   beside a narrower supporting panel; a quiet header keeps the document primary.
   Adopt a large editor with a separate, dismissible tutorial rail.
2. [Descript text editing, capture 2](https://images.refero.design/screenshots/descript.com/desktop/5bdabf4b-280e-4d07-9a71-8ba251290941/0.jpg):
   search metadata identifies text editing, sidebar/drawer, and side-sheet patterns.
   Treat these tags as a reference direction, not a detailed visual audit.
3. [Descript timeline workspace, capture 3](https://images.refero.design/screenshots/descript.com/desktop/860340f8-7816-4498-9672-fa0af22a2881/0.jpg):
   metadata identifies a persistent sidebar and timeline/history alongside content.
   ClearText uses a compact bottom status bar for secondary location information.

## Tokens

Extracted first-reference palette: `#313334`, `#95989a`, `#fbfafa`, `#d2e2ed`,
`#904acc`. Typeface metadata: San Francisco and Inter.

ClearText adapts the hierarchy rather than copying the palette. Current desktop
tokens: `#18212e` canvas, `#243144` surfaces, `#e6edf6` text, `#386eac` selection,
`#ffffff` light canvas, `#202b3c` light text. Segoe UI is used for interface text
and Consolas for documents. System fonts avoid runtime downloads.

## Applied patterns

- Document-first layout: editor expands; the guide remains a narrow rail.
- Persistent help: users can try actions while reading the tutorial beside them.
- Progressive disclosure: F1 hides guidance once it is familiar.
- Predictable navigation: labeled Back/Next controls and a step counter.
- Safe exploration: practice creates an unsaved tab rather than modifying a file.

An onboarding lookup using `get_onboarding(category="follow-product-tour")`
returned zero results and a taxonomy warning. No onboarding findings are claimed
from that lookup. The tutorial flow is an original design for this app.

## Validation and next steps

Layout measurements fit the editor and guide at 1220×800 and 960×560 for the
welcome step. Tutorial navigation, hide/show, and practice behavior are covered
by desktop tests. Full visual QA and a broader accessibility review remain open.

## Reader-first direction

The user asked for more than a reskinned editor. The current app combines a
paragraph Focus Lens, inferred heading map, bookmarks, locally remembered reading
positions, and estimated reading time. These are established patterns assembled
for a coherent plain-text reading workflow; no claim of first-of-its-kind
functionality is made. Sans / Book / Code use Segoe UI / Georgia / Consolas while
interface typography remains consistent. The sidebar and primary blue anchor
both themes, with separate accent text colors for dark-background readability.

Rendered native WPF previews of both themes and the compact window are inspected
locally. The application renders its own visual tree; screenshots do not capture
other desktop applications. Third-party reference images are not redistributed.
