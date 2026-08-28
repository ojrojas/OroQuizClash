let previouslyFocused = null;

const focusableSelector = [
    'a[href]',
    'button:not([disabled])',
    'input:not([disabled])',
    'select:not([disabled])',
    'textarea:not([disabled])',
    '[tabindex]:not([tabindex="-1"])'
].join(',');

export function activate(element) {
    if (!element) {
        return;
    }
    previouslyFocused = document.activeElement;
    const focusables = () => Array.from(element.querySelectorAll(focusableSelector));
    const first = focusables()[0];
    if (first) {
        first.focus();
    } else {
        element.setAttribute('tabindex', '-1');
        element.focus();
    }

    element.addEventListener('keydown', (event) => {
        if (event.key !== 'Tab') {
            return;
        }
        const items = focusables();
        if (items.length === 0) {
            event.preventDefault();
            return;
        }
        const firstItem = items[0];
        const lastItem = items[items.length - 1];
        if (event.shiftKey && document.activeElement === firstItem) {
            event.preventDefault();
            lastItem.focus();
        } else if (!event.shiftKey && document.activeElement === lastItem) {
            event.preventDefault();
            firstItem.focus();
        }
    });
}

export function deactivate() {
    if (previouslyFocused && typeof previouslyFocused.focus === 'function') {
        previouslyFocused.focus();
    }
    previouslyFocused = null;
}
