import { ApiService } from './services/api.js';
import { StateManager } from './services/state.js';
import { UIManager } from './ui/ui-manager.js';
import { TouchHandler } from './services/touch-handler.js';
import { Messages } from "./ui/messages.js";

class App {
    constructor() {
        this.state = new StateManager();
        this.api = new ApiService();
        this.ui = new UIManager();
        this.messages = new Messages();
        
        this.init();
    }

    async init() {
        try {
            this.state.userData = await this.api.ensureUser();
            this.updateMessages();
            await this.updatePicset();
            this.ui.setLoading(true);
            await this.loadNextImage();
            this.setupEventListeners();
        } catch (e) {
            console.error("Critical init error", e);
            //window.location.href = 'error.html?code=INIT_FAIL';
        }
    }
    
    async updatePicset() {
        const urlParams = new URLSearchParams(window.location.search);
        const picsetId = urlParams.get('picset');
        const picId = urlParams.get('picid');
        
        if (picsetId) {
            const picset = await this.api.getPictureSet(picsetId);
            this.state.currentSet.length = 0;

            if (picId) {
                const index = picset.indexOf(picId);
                if (index > -1) {
                    picset.splice(index, 1);
                    this.state.currentSet.push(picId);
                }
            }

            this.state.currentSet.push(...picset);
            this.state.currentSetIndex = 0;
            this.state.maxHistorySize = this.state.currentSet.length <= this.state.maxHistorySize ? this.state.currentSet.length : this.state.maxHistorySize;
            this.state.fullSetLoaded = this.state.currentSet.length === this.state.maxHistorySize;
        }
    }
    
    renderThumbItemCallback(div, itemId, itemIndex) {
        this.api.getThumb(itemId).then(b => {
                const url = URL.createObjectURL(b);
                div.innerHTML = `<img src="${url}" alt="Fav">`
                div.firstElementChild.onload = () => URL.revokeObjectURL(url);
            });
    }

    async preload(count) {
        if (count > this.state.maxHistorySize - 1) {
            count = this.state.maxHistorySize - 1;
        }
        
        console.log(`Preloading ${count} images`);
        for (let c = 0; c < count; c++) {
            await this.loadNextImageFromServer()
        }
    }
    
    async loadNextImageFromServer(token) {
        function fastHash(str) {
            let hash = 0;
            for (let i = 0; i < str.length; i++) {
                hash = (hash << 5) - hash + str.charCodeAt(i);
                hash |= 0;
            }
            return hash >>> 0;
        }
        
        let specificId = null;
        if (this.state.currentSet.length > 0 && this.state.currentSetIndex > -1 && this.state.currentSetIndex < this.state.currentSet.length) {
            specificId = this.state.currentSet[this.state.currentSetIndex];
            token = fastHash(`${specificId}`);
            if (++this.state.currentSetIndex === this.state.currentSet.length) {
                this.state.currentSetIndex = 0;
            }
        }
        if (!token) {
            token = Math.floor(Math.random() * 999999999);
        }
        
        const rawData = await this.api.getNextPicture(token, specificId);
        const picData = {
            ...rawData.headers,
            blobUrl: URL.createObjectURL(rawData.blob),
            token: token
        };

        this.state.pushToHistory(picData);
        return picData;
    }

    async loadNextImage() {
        // 1. Try to go forward in history
        const fullSetLoad = this.state.fullSetLoaded && this.state.historyStack.length === this.state.currentSet.length;
        const historyPic = this.state.moveForward(fullSetLoad);
        const maxPreloadCount = 5;
        const preloadTriggerIndex = fullSetLoad ? -100 : this.state.historyStack.length - 3;
        
        if (historyPic) {
            console.log(`Rendering image from history index: ${this.state.historyIndex}`);
            this.ui.renderImage(historyPic);
            
            if (this.state.historyIndex === preloadTriggerIndex) {
                const ignored = this.preload(maxPreloadCount);
            }
            
            return;
        }

        // 2. Fetch new
        this.ui.setLoading(true);
        try {
            console.log("Rendering image from server");
            const picData = await this.loadNextImageFromServer();
            this.ui.renderImage(this.state.updatePictureData(picData));
            const ignored = this.preload(maxPreloadCount);
        } catch (e) {
            console.error(e);
        } finally {
            this.ui.setLoading(false);
        }
    }

    goBack() {
        const prev = this.state.moveBack();
        if (prev) this.ui.renderImage(prev);
    }

    async handleVote(type) {
        const current = this.state.currentPicture;
        if (!current) return;

        const isUp = type === 'up';
        const isFav = type === 'fav';

        // Toggle logic
        let method = 'PUT';
        if ((isUp && current.upvoted) || (!isUp && !isFav && current.downvoted) || (isFav && current.isFav)) {
            method = 'DELETE';
        }

        // Optimistic UI Update
        if (isFav) {
            current.isFav = method === 'PUT';
            if (current.isFav) this.ui.playAudio('fav');
        } else {
            current.upvoted = isUp && method === 'PUT';
            current.downvoted = !isUp && method === 'PUT';
            
            if (method === 'PUT'){
                this.ui.playAudio(isUp ? 'like' : 'unlike');
            }
        }

        this.ui.updateButtons(current);

        // API Call
        const token = Math.floor(Math.random() * 999999999);
        await this.api.vote(current.id, type, method, token);

        if (isFav) this.updateFavoritesList();
    }

    async updateFavoritesList() {
        this.state.favorites = await this.api.getFavorites();
    }
    
    async openSetModal(setId, title) {
        const items = await this.api.getPictureSet(setId)
        if (!title) {
            title = this.messages.modalTitles[setId];
        }
        this.ui.renderGrid(items, title || setId, setId, (d, p, i) => this.renderThumbItemCallback(d, p, i));
        this.ui.openModal('fav');
    }

    setupEventListeners() {
        // Keyboard
        document.addEventListener('keydown', (e) => {
            if (e.key === 'ArrowRight') this.loadNextImage(); else
            if (e.key === 'ArrowLeft') this.goBack(); else
            if (e.key === 'ArrowUp') this.handleVote('up'); else
            if (e.key === 'ArrowDown') this.handleVote('down');  else
            if (e.key === 'Escape') this.ui.closeAllModals(); else
            if (e.key === 's' || e.key === 'S') this.ui.els.author.click(); else
            if (e.key === 'f' || e.key === 'F') this.openSetModal('my-favs'); else
            if (e.key === 't' || e.key === 'T') this.openSetModal('toprated'); else
            if (e.key === 'l' || e.key === 'L') this.openSetModal('lowrated'); else
            if (e.key === 'r' || e.key === 'r') this.ui.goHome(); else
            if (e.key === 'm' || e.key === 'M') {
                const currentPicture = this.state.currentPicture;
                if (currentPicture)
                    this.openSetModal(currentPicture.userId, currentPicture.author); 
            }
        });

        // UI Buttons
        this.ui.els.btnUp.onclick = (e) => { e.stopPropagation(); this.handleVote('up'); };
        this.ui.els.btnDown.onclick = (e) => { e.stopPropagation(); this.handleVote('down'); };
        this.ui.els.btnFav.onclick = (e) => { e.stopPropagation(); this.handleVote('fav'); };

        // Sidebar
        document.getElementById('hamburgerBtn').onclick = () => this.ui.toggleSidebar(true);
        document.getElementById('closeSidebar').onclick = () => this.ui.toggleSidebar(false);
        this.ui.els.sidebarOverlay.onclick = () => this.ui.toggleSidebar(false);

        document.getElementById('menuAbout').addEventListener('click', () => this.ui.openModal('about'));
        document.getElementById('closeAboutModal').addEventListener('click', () => this.ui.closeAllModals());
        document.getElementById('menuRandom').addEventListener('click', () => this.ui.goHome());
        document.getElementById('menuThemeToggle').addEventListener('click', () => this.ui.toggleTheme());

        // Modals
        window.addEventListener('click', (e) => e.target.id?.endsWith('Modal') ? this.ui.closeAllModals() : null);
        document.getElementById('closeInfoModal').onclick = () => this.ui.closeAllModals();
        document.getElementById('menuFavs').addEventListener('click', (e) => {this.openSetModal('my-favs');});
        document.getElementById('menuTop').addEventListener('click', async (e) => {this.openSetModal('toprated');});
        document.getElementById('menuLow').addEventListener('click', async (e) => {this.openSetModal('lowrated');});
        
        
        document.getElementById('infoBtn').addEventListener('click', (e) => {
            this.ui.openModal('info', (modal) => {
                const d = this.state.currentPicture;
                modal.querySelector('#modal-author').innerText = d.author;
                modal.querySelector('#modal-id').innerText = d.id;
                modal.querySelector('#modal-link').href = d.sourceUrl;
                const viewMoreLink = modal.querySelector('#view-more-link')
                viewMoreLink.href = `?picset=${d.userId}`;
                viewMoreLink.onclick = (e) => { e.preventDefault(); this.openSetModal(d.userId, d.author); }
            });
        });

        // Click
        if (!this.ui.isTouchScreen())
            this.ui.els.img.onclick = (e) =>
                (e.clientX < window.innerWidth / 2) ? this.goBack() : this.loadNextImage();
        
        // Touch
        new TouchHandler(document.getElementById('app-container'), {
            onUp: () => this.loadNextImage(),
            onDown: () => this.goBack(),
            onLeft: () => this.handleVote('down'),
            onRight: () => this.handleVote('up'),
            onHold: () => this.handleVote('fav')
        });
    }

    updateMessages() {
        this.ui.updateLabelText('menu-random', this.messages.menuRandom);
        this.ui.updateLabelText('menu-favs-label', this.messages.modalTitles['my-favs']);
        this.ui.updateLabelText('menu-top-label', this.messages.modalTitles['toprated']);
        this.ui.updateLabelText('menu-low-label', this.messages.modalTitles['lowrated']);
    }
}

// Start
document.addEventListener('DOMContentLoaded', () => new App());