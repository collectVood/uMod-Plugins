**Item Puller** gives you the possibilty of pulling items from your containers (boxes etc). To use it simply double click the item in the crafting tab (as if you want to craft it) which you want to pull the required ingredients for. It will then (if Item Puller is activated) look through your containers (lootables) in building priviledge radius. If it successfully finds all ingredients needed it will then move the items to your inventory, if enough slots are available. In case you have autocraft enabled it will instantly start crafting the selected item for you. If you are missing items it will show notice via a chat message which ingredient you are missing.

**Note:** In regards to **raiders**, if they get tool cupboard access they will be able to pull items from containers in the base as of right now, if "Check for Owners" is set to *false*. To protect against this I am planning to add future support for Clans and Friends API. As of right now you can simply activate the "Check for Owner". Also I might be adding another possible radius scan for servers with especially large bases, to save up some performance.

## Permissions

*  `itempuller.use` -- Allows players to use the Item Puller
*  `itempuller.forcepull` -- Allows players to force pull (spawns items)

## Chat Commands

* `/ip` - toggles item puller on/off
* `/ip <autocraft>` - toggles autocraft on/off
* `/ip <fromtc>` - toggle tool cupboard pulling on/off
* `/ip <fp>` - toggles force pulling on/off (spawning in items)
* `/ip <settings>` - shows your current settings

## Configuration

### Default Configuration

```json
{
  "Check for Owner (save mode)": false,
  "Player Default Settings": {
    "Enabled": false,
    "Autocraft": false,
    "Pull from ToolCupboard": true,
    "Force Pull (recommend to not set true)": false
  }
}
```

### Explanation

The **Check for Owner** is for everyone that wants to go the save route and only allow the owners of the lootables pulling from them then set this option to `true`. Otherwise the player can pull items from all lootables in his building priveledge zone. Players can not change this option. 

The **Player Default Settings** are the settings each player will get by default. That doesn't mean that he cannot change his setting though if one is disabled in the default player setting.

### Showcase
Plugin showcase video: https://youtu.be/BvLSaTXvKmg

## Credits

* **AnExiledGod** for helping with getting building info