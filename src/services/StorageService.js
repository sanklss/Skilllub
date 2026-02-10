export class StorageService {
    constructor() {
        this.authTokenKey = 'authToken';
        this.refreshTokenKey = 'refreshToken';
        this.userKey = 'user';
    }

    setAuthToken(token) {
        localStorage.setItem(this.authTokenKey, token);
    }

    getAuthToken() {
        return localStorage.getItem(this.authTokenKey);
    }

    setRefreshToken(token) {
        localStorage.setItem(this.refreshTokenKey, token);
    }

    getRefreshToken() {
        return localStorage.getItem(this.refreshTokenKey);
    }

    setUser(user) {
        localStorage.setItem(this.userKey, JSON.stringify(user));
    }

    getUser() {
        const user = localStorage.getItem(this.userKey);
        return user ? JSON.parse(user) : null;
    }

    clearAuth() {
        localStorage.removeItem(this.authTokenKey);
        localStorage.removeItem(this.refreshTokenKey);
        localStorage.removeItem(this.userKey);
    }

    isAuthenticated() {
        return !!this.getAuthToken() && !!this.getUser();
    }

    getUserRole() {
        const user = this.getUser();
        return user?.role || 'student';
    }
}