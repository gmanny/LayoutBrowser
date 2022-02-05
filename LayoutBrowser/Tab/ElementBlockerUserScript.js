{
    const blockStyleName = 'display';
    const blockStyleValue = 'none';
    const blockElementMarkerAttr = 'data-elblock-restore';
    const blockElementMarkerMagic = 'n/a';

    let docInited = false;
    let inited = false;
    let elementBlockRules = [];
    let debug = false; // this gets replaced before user script injection

    const reapplyRules = function(parentElements, rules = null) {
        if (rules === null) {
            rules = elementBlockRules;
        }

        for (const parentElement of parentElements) {
            for (const rule of rules) {
                const elements = [];
                try {
                    if (parentElement.matches && parentElement.matches(rule)) {
                        elements.push(parentElement);
                    } else { // no point inspecting inner elements if we block the parent itself
                        elements.push(...parentElement.querySelectorAll(rule));
                    }
                } catch (ex) {
                    if (debug) {
                        console.log('Error handling rule "', rule, '":', ex);
                    }
                }

                for (const element of elements) {
                    if (debug) {
                        console.log('Blocking element', element, 'with rule "', rule, '"');
                    }

                    const prevElementStyle = element.style[blockStyleName];
                    element.setAttribute(blockElementMarkerAttr, prevElementStyle ? prevElementStyle : blockElementMarkerMagic); // mark removed element
                    element.style[blockStyleName] = blockStyleValue;
                }
            }
        }
    }

    const handleMutation = function(mutationRecords) {
        const nodes = [];
        for (const mr of mutationRecords) {
            for (const node of mr.addedNodes) {
                if (node.nodeType !== 1 || node.parentElement === null) {
                    continue;
                }

                nodes.push(node);
            }
        }

        reapplyRules(nodes);
    };

    const observer = new MutationObserver(handleMutation);

    const handleNewRules = function(rules) {
        elementBlockRules.push(...rules);

        reapplyRules([document], rules);
    };

    const handleRemovedRules = function(rules) {
        const ruleSet = new Set(rules);
        const newRuleList = [];

        for (const rule of elementBlockRules) {
            if (ruleSet.has(rule)) {
                continue;
            }

            newRuleList.push(rule);
        }

        elementBlockRules = newRuleList;

        // restore removed elements
        for (const rule of rules) {
            try {
                const elements = document.querySelectorAll(rule);
                for (const element of elements) {
                    if (!element.hasAttribute(blockElementMarkerAttr)) {
                        continue;
                    }

                    const attrVal = element.getAttribute(blockElementMarkerAttr);
                    element.style[blockStyleName] = (!attrVal || attrVal === blockElementMarkerMagic) ? null : attrVal;
                }
            } catch (ex) {
                if (debug) {
                    console.log('Error restoring rule "', rule, '":', ex);
                }
                continue;
            }
        }
    };

    const handleMessage = function (msgContainer) {
        if (!msgContainer.data) {
            return;
        }

        const msg = msgContainer.data;

        if (debug) {
            console.log('WV MSG', msg);
        }

        if (!msg.type) {
            return;
        }

        if (msg.type === 'elementBlockInit') {
            if (!inited) { // avoid double init if, e.g. the page was refreshed multiple times
                if (msg.addedRules) {
                    handleNewRules(msg.addedRules);
                }

                // start observing DOM changes
                observer.observe(document, { subtree: true, childList: true });

                inited = true;
            }
        } else if (msg.type === 'elementBlockRulesChanged') {
            if (msg.removedRules) {
                handleRemovedRules(msg.removedRules);
            }

            if (msg.addedRules) {
                handleNewRules(msg.addedRules);
            }
        }
    };

    const init = function () {
        if (!window.chrome.webview || docInited) {
            return;
        }

        docInited = true;

        window.chrome.webview.addEventListener('message', handleMessage);

        // request rule set from application
        window.chrome.webview.postMessage({ type: 'elementBlockStart' });
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => init());
    } else {
        init();
    }
}