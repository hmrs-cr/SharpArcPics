import { ApiService } from './services/api.js';
import { StateManager } from './services/state.js';
import { UIManager } from './ui/ui-manager.js';
import { TouchHandler } from './services/touch-handler.js';

class App {
    constructor() {
        this.api = new ApiService();
        this.state = new StateManager();
        this.ui = new UIManager();

        this.init();
    }

    async init() {
        try {
            this.state.userData = await this.api.ensureUser();
            this.ui.setLoading(true);
            await this.loadNextImage();
            this.setupEventListeners();
        } catch (e) {
            console.error("Critical init error", e);
            window.location.href = 'error.html?code=INIT_FAIL';
        }
    }

    async loadNextImage() {
        // 1. Try to go forward in history
        const historyPic = this.state.moveForward();
        if (historyPic) {
            this.ui.renderImage(historyPic);
            return;
        }

        // 2. Fetch new
        this.ui.setLoading(true);
        const token = Math.floor(Math.random() * 999999999);
        try {
            const rawData = await this.api.getNextPicture(token);
            const picData = {
                ...rawData.headers,
                blobUrl: URL.createObjectURL(rawData.blob),
                token: token
            };

            this.state.pushToHistory(picData);
            this.ui.renderImage(picData);
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
            this.ui.playAudio(isUp ? 'like' : 'unlike');
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

    setupEventListeners() {
        // Keyboard
        document.addEventListener('keydown', (e) => {
            if (e.key === 'ArrowRight') this.loadNextImage();
            if (e.key === 'ArrowLeft') this.goBack();
            if (e.key === 'ArrowUp') this.handleVote('up');
            if (e.key === 'ArrowDown') this.handleVote('down');
            if (e.key === 'Escape') this.ui.closeAllModals();
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
        document.getElementById('menuRandom').addEventListener('click', () => window.location.href = '/');
        document.getElementById('menuThemeToggle').addEventListener('click', this.ui.toggleTheme);

        // Modals
        window.addEventListener('click', () => this.ui.closeAllModals());
        document.getElementById('closeInfoModal').onclick = () => this.ui.closeAllModals();
        document.getElementById('infoBtn').onclick = (e) => {
            e.stopPropagation();
            this.ui.openModal('info', (modal) => {
                console.log('info modal');
                const d = this.state.currentPicture;
                modal.querySelector('#modal-author').innerText = d.author;
                modal.querySelector('#modal-id').innerText = d.id;
            });
        };

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
}

// Start
document.addEventListener('DOMContentLoaded', () => new App());