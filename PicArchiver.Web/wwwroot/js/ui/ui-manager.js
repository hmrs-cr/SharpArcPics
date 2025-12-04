export class UIManager {
    constructor() {
        this.els = {
            img: document.getElementById('display-image'),
            author: document.getElementById('meta-author'),
            btnUp: document.getElementById('btnUp'),
            btnDown: document.getElementById('btnDown'),
            btnFav: document.getElementById('btnFav'),
            sidebar: document.getElementById('sidebar'),
            sidebarOverlay: document.getElementById('sidebarOverlay'),
            modals: {
                info: document.getElementById('infoModal'),
                about: document.getElementById('aboutModal'),
                fav: document.getElementById('favModal')
            },
            emos: {
                up: document.getElementById('emo-up'),
                down: document.getElementById('emo-down'),
                fav: document.getElementById('emo-fav')
            }
        };

        this.audio = {
            like: new Audio('/media/audio/like.mp3'),
            unlike: new Audio('/media/audio/unlike.mp3'),
            fav: new Audio('/media/audio/fav.mp3')
        };
        
        this.init();
    }

    isTouchScreen = () => 'ontouchstart' in document.documentElement;

    setLoading(isLoading) {
        if (isLoading) {
            this.els.img.classList.remove('loaded');
            this.els.author.innerText = "Loading...";
            this.els.btnUp.classList.remove('active');
            this.els.btnDown.classList.remove('active');
        }
    }

    renderImage(data) {
        this.els.img.src = data.blobUrl;
        this.els.img.alt = data.description || 'A picture!';
        document.title = this.els.img.alt;
        this.els.author.innerText = data.otherNames ? `${data.author} [...]` : data.author;
        this.els.author.href = data.sourceUrl;
        this.els.img.onload = () => this.els.img.classList.add('loaded');
        this.updateButtons(data);

        let topLabelText = '';
        if (data.imgIndex && data.imgCount) {
            topLabelText = `[${data.imgIndex}/${data.imgCount}]`
        }
        
        if (this.isTouchScreen()) {
            topLabelText = `${data.author} ${topLabelText}`;
        }

        this.updateLabelText('autor-label-top', topLabelText);
    }

    updateButtons(data) {
        this.toggleBtn(this.els.btnUp, this.els.emos.up, data.upvoted);
        this.toggleBtn(this.els.btnDown, this.els.emos.down, data.downvoted);
        this.toggleBtn(this.els.btnFav, this.els.emos.fav, data.isFav);
    }

    toggleBtn(btn, emo, isActive) {
        if (isActive) {
            btn.classList.add('active');
            emo.style.display = 'inline';
        } else {
            btn.classList.remove('active');
            emo.style.display = 'none';
        }
    }

    playAudio(type) {
        if (this.audio[type]) {
            this.audio[type].currentTime = 0;
            this.audio[type].play().catch(e => console.warn("Audio play failed", e));
        }
    }

    // Modal & Sidebar logic
    toggleSidebar(show) {
        const action = show ? 'add' : 'remove';
        this.els.sidebar.classList[action]('active');
        this.els.sidebarOverlay.classList[action]('active');
    }

    openModal(name, populateCallback) {
        if (this.els.modals[name]) {
            this.toggleSidebar(false);
            if (populateCallback) populateCallback(this.els.modals[name]);
            this.els.modals[name].style.display = 'flex';
        }
    }

    closeAllModals() {
        Object.values(this.els.modals).forEach(m => m.style.display = 'none');
    }

    toggleTheme() {
        const current = document.documentElement.getAttribute('data-theme');
        const newTheme = current === 'dark' ? 'light' : 'dark';
        document.documentElement.setAttribute('data-theme', newTheme);
        localStorage.setItem('theme', newTheme);
    }

    goHome() {
        window.location.href = '/';
    }

    goError(code) {
        window.location.href = '/error.html?code=' + code;
    }
    
    goToSet(setId, item) {
        let setUrl = `/?picset=${setId}`;
        if (item) {
            setUrl = `${setUrl}&picid=${item}`
        }
        window.location.href = setUrl;
    }

    updateLabelText(id, text) {
        const e = document.getElementById(id);
        e.textContent = text || e.textContent;
    }
    
    renderGrid(renderItems, title, setId, renderItemCallback) {
        const grid = document.getElementById('favGrid');
        const msg = document.getElementById('noFavsMsg');
        grid.innerHTML = '';

        if (!title) {
            title = `Total ${renderItems.length}`
        }
        else {
            title = `${title} (${renderItems.length})`
        }
        this.updateLabelText("favModalTitle", title);

        if (renderItems.length === 0) {
            msg.style.display = 'block';
            return;
        }

        console.log('setId1', setId);
        msg.style.display = 'none';
        renderItems.forEach((item, i) => {
            console.log('setId2', setId);
            const div = document.createElement('div');
            div.className = 'fav-item';
            renderItemCallback(div, item, i);
            div.onclick = async () => {
                console.log('setId3', setId);
                this.closeAllModals();
                this.toggleSidebar(false);
                this.goToSet(setId, item);
            };
            grid.appendChild(div);
        });
    }
    
    init() {
        const theme = localStorage.getItem('theme');
        if (theme) {
            document.documentElement.setAttribute('data-theme', theme);
        } else if ((window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches)) {
            document.documentElement.setAttribute('data-theme', 'dark');
        }
    }
}