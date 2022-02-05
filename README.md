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


# LayoutBrowser

A browser that specializes in creating information displays consisting of multiple websites.

LayoutBrowser provides several features to remove or hide clutter from web pages, such as element blocker, scroll/zoom lock and ability to set up and preserve a layout of overlapping windows with constant z-index. It uses Microsoft's Chromium Edge-based WebView2 as a browser engine.

# Feature highlights

* Preserves the layout of overlapping windows while letting you interact with any of them.
* Scroll and zoom retention.
* Page view expansion beyond window borders to hide headers, footers and side-bars (Negative margin).
* Removing elements based on CSS selector list.
* Unlimited easy to create browser profiles to separate session data between web pages. Supports different profiles for tabs in the same window :)
* Auto refresh based on timer.

# Other features

* URL lock - avoid needing to remember the window's URL when the website logs you out.
* Hide content - Easy way to temporarily hide page's content. You can hide the data that's distracting or stressful to always monitor and can easily show it again instead of hunting for the window button in the taskbar.
* Window/tab management you can expect from a day-to-day browser, facilitated by these hotkeys:
    * Ctrl+T for a new tab.
    * Ctrl+W to close current tab, or middle click on a tab to close it.
    * Ctrl+N for a new browser window.
    * Ctrl+Shift+T to reopen a previously closed tab or window. By default, LayoutBrowser doesn't retain the history of closed tabs and windows after restart, for privacy reasons, but you can enable this behavior if you're used to it from other browsers by toggling "Store closed tab history" setting.
    * Ctrl+Tab/Ctrl+Shift+Tab switches to the next or previous tab in a window.
    * Ctrl+Alt+Shift+Left/Ctrl+Alt+Shift+Right moves current tab left or right among other tabs in a window.
    * Ctrl+Shift+P pops the current tab out into a new window.
    * F5 to refresh and Escape to stop page load.
    * F6 to move the focus to the address bar.
    * F12, of course, opens Developer Tools.
* Page title override - Change the title of the page displayed in the header and taskbar. Can be useful for making tabs more distinct in a multi-tab window, or similarly windows easier to find in a taskbar.
* Similarly, you can assign each window an icon from an `.ico` file to make it easy to find even in the most crowded of taskbars.
* Sometimes, the browser process that renders the web page crashes and LayoutBrowser automatically reloads the web page in this case. If this causes a problem, you can disable this behavior per-tab by toggling the "Don't refresh on browser fail" function on. To help you catch an infrequent crash, this menu item will also display the time and date of last crash if it occurs.
* Multi-DPI monitor setup support.

# Some feature guides

* Layout setup and preservation. ... When you need to interact with some part of the window that's obscured by other windows in a locked layout, activate that window and press Ctrl+Shift+F to temporarily bring it to front.
    * List all of the options where windows can be excepted from the fixed layout. How the new created windows are excepted. That each window can be tweaked to use to-front or to-back, etc...
* zoom retention - you can change zoom by Ctrl+Plus/Ctrl+Minus. It is restored after restart.
* scroll restore and lock - if the page you're viewing loads data after load, you can set a delay for scroll restore. you can also lock scroll to a current position so that any future scroll position change done either by you or the web page is discarded after the page refresh. This feature is intended for use with page auto refresh.
* URL lock - it remembers the URL that was opened in the tab when you enabled it. Log in using the web form and LayoutBrowser will redirect you to a URL that was previously opened. The login flow consists of more than one page? Turn the URL lock feature off, log in and then turn the URL lock feature on by middle-clicking its feature button on the address bar to restore the previously saved URL instead of remembering the new one.
* Scroll lock - normally, LayoutBrowser stores the page's scroll position just before a refresh occurs and restores the newly loaded page to the same position. Scroll lock remembers the scroll position of the web page when it was enabled and always restores the web page to that scroll position on refresh, ignoring any scroll changes done by the user or the webpage itself.
* Hide content - in addition to toggling it in the settings menu, you can also toggle it by right-clicking the window's Minimize button. You can unhide hidden content by right-clicking anywhere on the hidden-content area.
* Negative margin - in a normal mode it applies negative margin to left, right and top of the page's `body` element. The bottom border is always done in a "native" mode, in which a browser control is extended below the window's border. This is done since there is no point in applying negative margin to the `body` element and fixed bottom panels can't be hidden by it. As such, "pixels" of the Bottom and Top negative margins represent different kinds of logical pixel dimensions and may not be comparable, especially when the web page is zoomed in or out. You can toggle "native" margin mode for left or right borders in case the web page does not react to a negative CSS margin as expected.
* When press a close window button, it doesn't quit the browser, the window is removed from layout instead. If it was not intended, you can press Ctrl+Shift+T to reopen this window. To quit the browser so that it retains all of the open windows on restart, either choose a Quit option in the settings menu, press Ctrl+Q or middle-click any window's close button.
* Does Element blocker even need a guide?

# FAQs and known issues

* Windows-only --- the window z-index preservation code is Windows-specific and actually really works nicely only with hardware-accelerated DWM rendering, so using it under Wine or in Windows 7 with classic themes may lead to degraded drawing performance and incorrect behavior.
* Negative margin doesn't support native mode for the top boundary --- sometimes the page displays its header in a way where placing a negative margin on a `body` element doesn't help with hiding it. Unfortunately, they "native" mode of negative margin that's supported for left, right and bottom borders, is not currently supported for the top border because of the way Microsoft's WebView2 implements integration into WPF. When expanded above beyond its view boundary, the browser control would, in its current state, overlap the address bar and window header, preventing it from being used. This is a much-requested feature for WebView2, it's tracked [here](https://github.com/MicrosoftEdge/WebView2Feedback/issues/197), [here](https://github.com/MicrosoftEdge/WebView2Feedback/issues/23) and [maybe here](https://github.com/MicrosoftEdge/WebView2Feedback/issues/20).
* Negative margin in native mode leads to the browser control overlapping window's resize borders and makes it unusable. This is due to how WebView2 is integrated with WPF, check the item above for more info.
* Why so many hotkeys for moving/popping out a tab instead of drag and drop? Drag and drop support requires more time investment to get right, and those tab management features are not really heavily used in the use case that this application targets. They are provided as hotkeys for the times they are required, but I understand that drag and drop is much more convenient and quick. If you've got time, I would appreciate the contribution of drag and drop for tabs immensely.
* When you move a window to a monitor with DPI different from where it's originated, the contents of the browser will be interpolated until the browser is restarted. This is a behavior of WebView2 control this browser is using. To get a crisp picture without a restart, move the window to its intended monitor and duplicate it, closing the original window after.