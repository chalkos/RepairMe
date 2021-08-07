# RepairMe

Plugin for XivLauncher/Dalamud that helps you notice when your gear needs to be repaired. It shows the condition of your most-damaged piece of gear and alerts when it's getting low.

## Commands

- `/repairme` - opens the config window

## Features

- Display a bar that shows the condition of your most broken item, and near it a number showing the exact condition percentage
    - Can display only the bar, only the percentage, or none (to only show alerts)
    - Bar size is configurable
- Display alerts (text) when the condition is low or critical (condition low and critical thresholds are configurable)
    - Alerts' can be positioned independently of the condition bar, or even hidden completely
- Bar and alerts are clickthrough, but can be moved by unlocking them in the config. When unlocked they will have a red background showing the draggable area (lock them to hide the red background)
- With the config open, testing mode simulates different gear condition percentages to allow adjusting the colors without the need to equip gear with lower condition
- Almost all text and background colors can be configured