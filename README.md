# RepairMe

Plugin for XivLauncher/Dalamud that helps you notice when your gear needs to be repaired. It shows the condition of your most-damaged piece of gear and alerts when it's getting low.

## Commands

- `/repairme` - opens the config window

## Features

- Display a bar that shows the condition of your most broken item, and near it a number showing the exact condition percentage
    - Can display only the bar, only the percentage, or none (to only show alerts)
    - Bar size & orientation are configurable
- Display alerts (text) when the condition is low or critical (condition low and critical thresholds are configurable)
    - Alerts' can be positioned independently of the condition bar, or even hidden completely
- Bar and alerts are clickthrough, but can be moved by unlocking them in the config. When unlocked they will have a red background showing the draggable area (lock them to hide the red background)
- With the config open, testing mode simulates different gear condition percentages to allow adjusting the colors without the need to equip gear with lower condition
- Almost all text and background colors can be configured

## Changelog

* v1.0.0.3
  * Now supports more than 100% condition
* v1.0.0.2
  * Fix `System.AccessViolationException` in `GetConditionInfo()`
    * shouldn't crash on logout/disconnect anymore
* v1.0.0.1
  * No longer version zero!
  * New config: bar orientation
    * Left to right by default (as it was in previous versions)
    * Mirror effect can be obtained by selecting "Right to left"
  * Disabled docking for bars, percentages and alerts
    * This fixes a bug that happened when resizing one of those components while docked without "Move UI" turned on. These minimalistic widgets arent meant to be docked anyway, so no more of this dirty docking business.
* v0.2.0.4
  * Disable inputs for colors only on the config window (was disabling globally)
  * Config window can now be collapsed without errors
* v0.2.0.2
  * Try to have images shown in Dalamud
* v0.2.0.1
  * Fix loading configuration
* v0.2.0.0
  * Upgrade to Dalamud API4