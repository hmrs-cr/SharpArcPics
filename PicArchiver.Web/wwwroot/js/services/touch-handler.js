export class TouchHandler {
    constructor(element, callbacks) {
        this.element = element;
        this.callbacks = callbacks; // { onUp, onDown, onLeft, onRight, onHold }
        this.startX = 0;
        this.startY = 0;
        this.pressTimer = null;

        this.init();
    }

    init() {
        if (!('ontouchstart' in document.documentElement)) return;

        this.element.addEventListener('touchstart', (e) => this.start(e), {passive: false});
        this.element.addEventListener('touchend', (e) => this.end(e));
        this.element.addEventListener('touchcancel', () => clearTimeout(this.pressTimer));
    }

    start(e) {
        this.startX = e.changedTouches[0].screenX;
        this.startY = e.changedTouches[0].screenY;

        // Long press detection
        this.pressTimer = setTimeout(() => {
            if (this.callbacks.onHold) this.callbacks.onHold();
        }, 600);
    }

    end(e) {
        clearTimeout(this.pressTimer);
        const endX = e.changedTouches[0].screenX;
        const endY = e.changedTouches[0].screenY;
        this.handleGesture(endX - this.startX, endY - this.startY);
    }

    handleGesture(diffX, diffY) {
        const threshold = 55;
        if (Math.abs(diffX) > Math.abs(diffY)) {
            if (Math.abs(diffX) > threshold) {
                diffX > 0 ? this.callbacks.onRight() : this.callbacks.onLeft();
            }
        } else {
            if (Math.abs(diffY) > threshold) {
                diffY > 0 ? this.callbacks.onDown() : this.callbacks.onUp();
            }
        }
    }
}