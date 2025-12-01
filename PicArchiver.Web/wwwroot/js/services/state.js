export class StateManager {
    constructor() {
        this.historyStack = [];
        this.historyIndex = -1;
        this.currentSet = [];
        this.currentSetIndex = -1;
        this.favorites = [];
        this.userData = null;
    }

    get currentPicture() {
        return this.historyIndex >= 0 ? this.historyStack[this.historyIndex] : null;
    }

    pushToHistory(data) {
        // If we are in the middle of history and load new, truncate forward history
        if (this.historyIndex < this.historyStack.length - 1) {
            this.historyStack = this.historyStack.slice(0, this.historyIndex + 1);
        }

        this.historyStack.push(data);
        this.historyIndex++;
        this.manageMemory();
    }

    moveBack() {
        if (this.historyIndex > 0) {
            this.historyIndex--;
            return this.currentPicture;
        }
        return null;
    }

    moveForward() {
        if (this.historyIndex < this.historyStack.length - 1) {
            this.historyIndex++;
            return this.currentPicture;
        }
        return null; // Indicates need to fetch new
    }

    manageMemory(maxItems = 30) {
        if (this.historyStack.length > maxItems) {
            const removed = this.historyStack.shift();
            if (removed.blobUrl) URL.revokeObjectURL(removed.blobUrl);
            this.historyIndex--;
        }
    }

    updateCurrentVoteState(up, down, fav) {
        const p = this.currentPicture;
        if(p) {
            if(up !== null) p.upvoted = up;
            if(down !== null) p.downvoted = down;
            if(fav !== null) p.isFav = fav;
        }
    }
}