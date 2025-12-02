export class StateManager {
    constructor() {
        this.historyStack = [];
        this.historyIndex = 0;
        this.currentSet = [];
        this.currentSetIndex = -1;
        this.favorites = [];
        this.userData = null;
        this.maxHistorySize = 30;
        this.fullSetLoaded = false;
    }

    get currentPicture() {
        const pic = this.historyIndex >= 0 ? this.historyStack[this.historyIndex] : null;
       return this.updatePictureData(pic);
    }
    
    updatePictureData(pic) {
        if (pic) {
            if (this.currentSet.length > 0) {
                pic.imgCount = this.currentSet.length;
                if (!pic.imgIndex) {
                    pic.imgIndex = this.currentSet.indexOf(pic.id) + 1;
                }
            }
        }
        return pic;
    }

    pushToHistory(data) {
        this.historyStack.push(data);
        this.trimHistory();
    }
    
    trimHistory() {
        if (this.historyStack.length >= this.maxHistorySize) {
            const i = this.historyStack.length - this.maxHistorySize;
            if (i > 0) {
                const removed = this.historyStack.splice(0, i);
                removed.forEach(i => {
                    if (i.blobUrl) {
                        URL.revokeObjectURL(i.blobUrl);
                        console.debug(`Removed ${i.blobUrl}`);
                    }
                });
                this.historyIndex = this.historyIndex - i;
                console.log(`History too big, removed ${i} items at the beginning, new index is ${this.historyIndex}, history lenght ${this.historyStack.length}`);
            }
        }
    }

    moveBack() {
        if (this.historyIndex > 0) {
            this.historyIndex--;
            return this.currentPicture;
        }
        return null;
    }

    moveForward(circle) {
        if (this.historyIndex < this.historyStack.length - 1) {
            this.historyIndex++;
            return this.currentPicture;
        } else if (circle) {
            this.historyIndex = 0;
            return this.currentPicture;
        }
        
        return null; // Indicates need to fetch new
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