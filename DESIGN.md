---
name: TXATOOL - WIN
description: A warm-stone Windows toolkit with light and dark themes, translucent panes, solid content cards and one burnt-orange accent.
colors:
  accent-orange: "#D9480F"
  action-orange-light: "#C2410C"
  action-orange-dark: "#D9480F"
  accent-text-light: "#C2410C"
  accent-text-dark: "#FB923C"
  accent-soft-light: "#FFF1E7"
  accent-soft-dark: "#3A2418"
  ground-light-top: "#F6F3EF"
  ground-light-bottom: "#E9E3DB"
  ground-dark-top: "#171412"
  ground-dark-bottom: "#0F0D0C"
  surface-light: "#FFFFFF"
  surface-dark: "#292422"
  surface-alt-light: "#FAF8F6"
  surface-alt-dark: "#221E1C"
  line-light: "#E3DDD5"
  line-dark: "#3F3935"
  text-light: "#1C1917"
  text-dark: "#F5F2EE"
  text-secondary-light: "#44403C"
  text-secondary-dark: "#D6D0C9"
  text-muted-light: "#6B645D"
  text-muted-dark: "#A59D95"
typography:
  title:
    fontFamily: "Segoe UI"
    fontSize: "22px"
    fontWeight: 600
  nav:
    fontFamily: "Segoe UI"
    fontSize: "17px"
    fontWeight: 500
  body:
    fontFamily: "Segoe UI"
    fontSize: "15px"
    fontWeight: 400
  label:
    fontFamily: "Segoe UI"
    fontSize: "14px"
    fontWeight: 600
rounded:
  xs: "4px"
  sm: "8px"
  md: "10px"
  lg: "12px"
  xl: "15px"
  pill: "21px"
spacing:
  xs: "5px"
  sm: "10px"
  md: "15px"
  lg: "20px"
components:
  nav-item:
    backgroundColor: "transparent"
    textColor: "{colors.text-light}"
    typography: "{typography.nav}"
    rounded: "{rounded.md}"
    height: "36px"
  button-action:
    backgroundColor: "{colors.accent-soft-light}"
    textColor: "{colors.action-orange-light}"
    rounded: "{rounded.lg}"
    padding: "12px 25px"
  content-card:
    backgroundColor: "{colors.surface-light}"
    rounded: "{rounded.lg}"
---

# Design System: TXATOOL - WIN

## Overview

**Creative North Star: "The Warm Workbench"**

A technician's bench in a lit workshop: warm stone ground, translucent working panes laid on it, white (or raised stone) cards for the things you read, and one burnt-orange accent for what is selected or can be pressed. It is meant to be used for hours, in a shop with strong overhead light or late at night, so it ships as a light and a dark theme that follow Windows by default.

Orange replaces the previous Win11-blue identity on purpose: it separates TXATOOL - WIN from the default Windows look and from other Windows tool suites. The wallpaper background is gone; the ground is a quiet two-stop gradient so both themes read the same way.

**Key Characteristics:**
- Theme-aware by construction: every colour in XAML is a `DynamicResource` key from `Themes/Light.xaml` or `Themes/Dark.xaml`. Never hard-code hex.
- Two surface levels: translucent panes (sidebar, main, description) and solid content cards on top of them.
- One accent family (orange). State colours (green, amber, red) appear only where they carry meaning.
- Rounded, flat, no decorative shadows. Segoe UI and Segoe Fluent Icons throughout.
- Theme is Light, Dark or Follow system (default). Choice is stored in `cfg.conf` and applied before the first window loads.

## Colors

Warm stone neutrals with a single orange family. Light and dark values are paired; key names are identical in both dictionaries.

### Primary
- **Accent Orange** (#D9480F, both themes): toggle on-state, focus rings, active bars. Theme-invariant; reads on both grounds.
- **Action Orange** (#C2410C light, #D9480F dark): solid fills that carry white text, icons on cards, selection marks. Light value keeps white text at about 5:1.
- **Accent Text** (#C2410C light, #FB923C dark): orange used as text or link on a surface.
- **Accent Soft** (#FFF1E7 light, #3A2418 dark) with hover and pressed steps: tinted button and highlight fills.

### Neutral
- **Ground** gradient (#F6F3EF to #E9E3DB light; #171412 to #0F0D0C dark): the window background.
- **Panel** (translucent white or stone) with hover and selected steps: sidebar, main and description panes, nav item states.
- **Surface** (#FFFFFF light, #292422 dark), **Surface Alt**, **Surface Sunken**: cards, inputs, tracks.
- **Line** (#E3DDD5 / #3F3935) and **Line Strong**: borders and dividers.
- **Text**, **Text Secondary**, **Text Muted**: three text levels, all at 4.5:1 or better on their surfaces.

### Named Rules
**The Key Rule.** XAML and code reference theme brushes by key (`{DynamicResource TextBrush}`, `ThemeBrushes.Get("LineBrush")`). A hex literal for text, surface, border or accent is a defect.
**The One Family Rule.** Orange is the only chromatic highlight. Green, amber and red mean state, nothing else.
**The Animation Exception.** Storyboards cannot use dynamic resources; they use the invariant `AccentColor`.

## Typography

**Font:** Segoe UI (system). **Icons:** Segoe Fluent Icons with Segoe MDL2 Assets fallback. No emoji or Unicode symbols as icons.

- **Title** (600, 22px): app name in the header.
- **Nav** (500, 17px; 600 selected): sidebar items and the header action.
- **Body** (400, 15px): default text; toggle labels 17px.
- **Label** (600, 14px): dropdowns, action buttons, checkboxes, radios, group headers (12px, 600, muted).

## Layout

Fixed 1600x900 design canvas inside a `Viewbox`. Header 60px; sidebar 280px; flexible main area; 320px description panel. Panes are inset 15-20px from the window edge and 10px from each other. Rhythm: 5 / 10 / 15 / 20px.

The sidebar is grouped, with a small muted header per group: Dashboard on its own, then Optimize, Software, Licensing, Devices, Fix errors. All nav items share one style (`MenuButtonStyle`) with a Segoe Fluent glyph.

## Elevation & Depth

Flat. Depth comes from the difference between translucent panes and solid cards, plus a 1px Line border on cards. Shadows appear only on tooltips and the toggle knob.

## Shapes

Window and panes 15px; cards, buttons and dropdowns 12px; nav items 10px; tooltips 8px; checkboxes 4px; search and header action are pills (18-21px); toggle track is fully round.

## Components

### Navigation (sidebar)
10px rows, 36px high, glyph left. Hover uses Panel Hover, selected uses Panel Selected with an orange icon. Keyboard focus draws a 2px Action Orange ring (the border is always 2px and transparent at rest, so focus never shifts layout).

### Buttons
- **Action:** Accent Soft fill, orange text, 12px radius; hover and pressed step through Accent Soft Hover and Pressed.
- **Solid confirm:** Action Orange fill with white text.
- **Header pill and window controls:** Panel fills; glyph icons (14px).

### Cards (Dashboard and content)
Solid Surface fill, 1px Line border, 12px radius. This is the rule that settles the earlier white-card-on-glass mismatch: panes are translucent, anything you read or edit sits on a solid card.

### Toggle, dropdown, checkbox, radio
All focusable with a visible ring. Toggle on-state is Accent Orange. Dropdown popups use Surface with Line border.

## Do's and Don'ts

### Do:
- **Do** add new colours to both `Themes/Light.xaml` and `Themes/Dark.xaml` under the same key.
- **Do** test every new view in Light and Dark and in Vietnamese and English (strings come from `Resources/Language/*.json`; add both).
- **Do** keep nav items on `MenuButtonStyle` and use a Segoe Fluent glyph via `Tag`.
- **Do** give icon-only buttons an `AutomationProperties.Name` and a tooltip.

### Don't:
- **Don't** hard-code hex for text, surface, border or accent, in XAML or C#.
- **Don't** add shadows to panes or cards.
- **Don't** use emoji or Unicode symbols as icons.
- **Don't** use orange for state; green, amber and red keep their meanings.
