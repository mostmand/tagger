---
name: tagger-ui
description: Avalonia UI conventions and visual language for Tagger. Use when building or changing views, view models, styles, dialogs, or theme resources.
---

# Tagger UI rules

## Stack

- Avalonia 12.1.x, .NET 11, CommunityToolkit.Mvvm, compiled bindings.
- `x:DataType` on every view. No reflection bindings.
- View models in `Tagger.Desktop/ViewModels`; views in `Tagger.Desktop/Views`.
- Dependencies are injected. Views never construct services.

## Visual language

- Dark-first. Background `#0B0F14`, surface `#11161D`, raised `#161D26`, border `#232C38`.
- Text `#E8EEF5`, muted `#8A97A8`, accent `#6C8CFF`, success `#3DDC97`, danger `#FF6B81`.
- Inter font, 12–14 px body, 11 px uppercase section labels with letter spacing.
- 8–12 px corner radii, 1 px borders, no heavy shadows, no gradients on controls.
- Dense table rows (40–44 px), clear selected state, generous whitespace only around groups.
- Use `Themes/Tokens.axaml` resources. Never hard-code a color in a view.

## Interaction

- Primary action is always visible (Preview & save).
- Destructive actions require confirmation; tag operations require a preview.
- Mixed values show a distinct state and are only written when the user edits the field.
- Busy state disables mutating commands; errors surface in the status bar and as dialogs.
- Keyboard: Ctrl+O add folder, Ctrl+S save, Ctrl+F search, F2 rename, Delete unload selection.
- Empty states explain the next action and expose it as a button.

## Code-behind

Allowed: `InitializeComponent`, focus management, window drag/chrome, forwarding a view-only
event to a command. Not allowed: business rules, tag manipulation, service calls.
