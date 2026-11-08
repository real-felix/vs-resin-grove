# Changelog

## 1

First public release. Game metadata uses the required semantic version `1.0.0`.

- Build and validate installable ZIPs through GitHub Actions.
- Create draft GitHub releases when matching version tags are pushed.
- Include `modicon.png` and the license in packages when present.

- Follow vanilla resin regrowth intervals by default, including patched per-species values.
- Keep positive `HoursPerResin` settings as explicit overrides; use `null` to follow vanilla.

- Smith two metal tree taps from one ingot.
- Tap living pine and acacia trees, with one tap per connected tree.
- Hang vanilla wooden buckets to collect resin over time.
- Preserve tap ownership and bucket progress through save/reload.
