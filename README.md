# LayoutBrowser

A (Chromium) Edge wrapper that specializes in creating information displays consisting of multiple websites.

## Key features

* Restores window layout between restarts and maintains window z-order when windows are interacted-with.
* User interface can be hidden to leave only window header and content.
* Timer-based page auto-refresh.
* Multiple browser user profiles for data/tracking separation between websites.
* Maintains scroll position between page refreshes with customizable delay to account for dynamically loaded content.
* Ability to apply negative margin to crop into the relevant part of the page and hide headers/footers/sidebars.

## Nice-to-haves

* Tab management within one window - in case you need it.
* Easy way to temporarily hide page's content. You can hide the data that's distracting or stressful to always monitor and can easily show it again instead of hunting for the window button in the taskbar.
* Ability to assign a custom icon to each browser window. Makes it easier to find a specific window in the Taskbar.
* Ability to override page title displayed by the tab/window header. Have multiple tabs opened, but the page title is not informative or contains unneeded text - this solves it.

## Some features in-depth

### Layout maintenance

The browser remembers the position of its windows and their order on the screen and restores it after the restart. At any point, the **Window order locked** mode can be enabled to lock the z-order of windows. After that any newly activated window doesn't become topmost and thus doesn't ruin content overlap that windows can have.

When *window order is locked*, the way that LayoutBrowser maintains window z-order can be toggled with the **Maintain order with ToBack** setting. If this setting is *turned off*, the browser will send all of its windows to foreground, thus obstructing any other running application windows. If it's *turned on*, the opposite happens, with browser windows being sent to background, thus getting out of the way of any other running applications. You can choose one or another depending on the importance of the content displayed by the browser.

Also, when *window order is locked*, each individual window can be toggled as **Not in layout**. This takes it out of the z-order restoration stack and allows you to use it as a normal browser window. All newly opened windows are automatically *Not in layout* when *window order is locked*.

### Negative margin

You can apply negative offset to the page content 