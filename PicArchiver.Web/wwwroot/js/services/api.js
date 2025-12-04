export class ApiService {
    constructor() {
        this.userId = localStorage.getItem('userId');
        this.baseUrl = ''; // Relative path based on your existing code
    }

    get headers() {
        return { 'uid': this.userId || '' };
    }

    async ensureUser() {
        if (this.userId) {
            const res = await fetch('/user', { headers: this.headers });
            if (res.ok) return await res.json();
            console.warn(`User '${this.userId}' invalid, creating new.`);
        }

        // Retry logic for creating user
        for (let i = 0; i < 5; i++) {
            const res = await fetch('/user', { method: 'POST' });
            if (res.ok) {
                const data = await res.json();
                this.userId = data.id;
                localStorage.setItem('userId', this.userId);
                return data;
            }
        }
        throw new Error('Failed to create user');
    }

    async getPictureSet(setName) {
        const res = await fetch(`/picture/set/${setName}`, { headers: this.headers });
        return res.ok ? await res.json() : [];
    }

    async getNextPicture(token, specificId = null) {
        const url = specificId
            ? `/picture/${specificId}?token=${token}`
            : `/picture/next?token=${token}`;

        const res = await fetch(url, { headers: this.headers });
        if (!res.ok) throw new Error('Failed to fetch image');

        return {
            blob: await res.blob(),
            headers: {
                author: res.headers.get('IG_UserName'),
                otherNames: res.headers.get('IG_OtherUserNames'),
                userId: res.headers.get('IG_UserId'),
                id: res.headers.get('InternalId'),
                isFav: Number(res.headers.get('IsFav')),
                upvoted: Number(res.headers.get('Upvoted')),
                downvoted: Number(res.headers.get('Downvoted')),
                sourceUrl: res.headers.get('SourceUrl'),
                description: res.headers.get('Description')
            }
        };
    }

    async vote(id, type, method, token) {
        // type: 'up', 'down', 'fav'
        // method: 'PUT' (add), 'DELETE' (remove)
        await fetch(`/picture/${id}/${type}?token=${token}`, {
            method: method,
            headers: this.headers
        });
    }

    async getFavorites() {
        const res = await fetch('/user/favs', { headers: this.headers });
        return res.ok ? await res.json() : [];
    }

    async getThumb(id) {
        const res = await fetch(`/picture/${id}/thumb`, { headers: this.headers });
        return res.ok ? await res.blob() : null;
    }
}