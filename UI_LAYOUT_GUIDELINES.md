# UI layout guidelines

This app uses Fluent UI Blazor v5 for controls, while CSS Grid and Flexbox define page structure and alignment. Fluent components do not decide where each page's fields, actions, and sections belong. Use the shared layout classes below so pages stay aligned as content and window sizes change.

## Page structure

- Keep one clear page heading, then group content into toolbars, summary cards, and the main table or form.
- Use consistent heading margins and the shared route padding from `MainLayout.razor.css`.
- Use `FluentStack` for a short, simple row or column where its built-in gap is all that is needed. Use a plain `div` with a named CSS class when the layout needs grid columns, wrapping, independent action alignment, or label-aware alignment.
- Prefer `FluentGrid` for content grids whose responsive column spans are meaningful. Do not add fixed minimum widths that can exceed the available viewport.

## Forms and toolbars

- Put each field's label above its control. Align fields with buttons at the control's bottom edge so labels do not shift the control surface.
- Wrap toolbar controls in the shared `ToolbarField` component. Use it without parameters for controls with visible labels, `Unlabeled` for unlabeled controls or buttons, and `Unlabeled Search` for list search fields. For example:

  ```razor
  <ToolbarField Unlabeled Search>
      <FluentTextInput Placeholder="Filter items" class="list-filter" />
  </ToolbarField>
  ```

- The component applies `.toolbar-field`, `.toolbar-field-unlabeled`, and `.toolbar-field-search`. Shared CSS resets FluentField's default outer margin and reserves the label row so controls align by their visible surfaces.
- For list pages use `.list-toolbar-layout`, `.list-toolbar-filters`, `.list-toolbar-actions`, `.list-filter`, and `.list-select` from `app.css`.
- Use `ToolbarField` inside `.project-folder-row` and `.pattern-toolbar-layout` for consistent button alignment.
- Keep field widths in shared CSS classes. Avoid inline `width`, `min-width`, margins, and per-page gap values for common page layouts.
- When a toolbar becomes too narrow, let filters wrap and move actions below the filters. Preserve usable minimum control widths without forcing horizontal overflow.

## Summary cards

- Add `class="metric-grid"` to a `FluentGrid` used for summary cards.
- Use the `.metric-label` class for card labels. Let cards share the grid's available width and avoid hard minimum widths on the card or grid item.
- Keep labels and values in the same order and use matching spacing across pages.

## Review checklist

- Do the input surfaces line up even when some controls have labels and others do not?
- Do action buttons align with the controls and wrap cleanly at small widths?
- Do cards share consistent widths and remain inside the content area?
- Are repeated widths and spacing expressed by shared classes rather than repeated inline styles?
- Check at desktop and narrow-window widths after changing a shared layout rule.

This guidance follows the Fluent UI Blazor v5 component model: use Fluent controls and providers for Fluent UI behavior, then use normal CSS layout for application-specific composition. It does not require every page to hand-position each control.
