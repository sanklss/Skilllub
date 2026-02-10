import { API_URL } from '../config/constants.js';
import { StorageService } from './StorageService.js';

export class ApiService {
    constructor() {
        this.storage = new StorageService();
        this.baseURL = API_URL;
    }

    async request(endpoint, options = {}) {
        const url = `${this.baseURL}${endpoint}`;
        const config = {
            headers: {
                'Content-Type': 'application/json',
                ...options.headers
            },
            ...options
        };

        const token = this.storage.getAuthToken();
        if (token) {
            config.headers['Authorization'] = `Bearer ${token}`;
        }

        try {
            const response = await fetch(url, config);
            
            if (response.status === 401) {
                const refreshed = await this.refreshToken();
                if (refreshed) {
                    const newToken = this.storage.getAuthToken();
                    config.headers['Authorization'] = `Bearer ${newToken}`;
                    return await fetch(url, config);
                } else {
                    this.storage.clearAuth();
                    throw new Error('Сессия истекла');
                }
            }

            return response;
        } catch (error) {
            console.error('API Request failed:', error);
            throw error;
        }
    }

    async refreshToken() {
        try {
            const refreshToken = this.storage.getRefreshToken();
            if (!refreshToken) return false;

            const response = await fetch(`${this.baseURL}/auth/refresh-token`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    accessToken: this.storage.getAuthToken(),
                    refreshToken: refreshToken
                })
            });

            if (response.ok) {
                const data = await response.json();
                if (data.success) {
                    this.storage.setAuthToken(data.token);
                    this.storage.setRefreshToken(data.refreshToken);
                    return true;
                }
            }
            return false;
        } catch (error) {
            console.error('Token refresh failed:', error);
            return false;
        }
    }

    async login(email, password) {
        const response = await this.request('/auth/login', {
            method: 'POST',
            body: JSON.stringify({ Email: email, Password: password })
        });
        return response.json();
    }

    async register(userData) {
        const response = await this.request('/auth/register', {
            method: 'POST',
            body: JSON.stringify(userData)
        });
        return response.json();
    }

    async forgotPassword(email) {
        const response = await this.request('/auth/forgot-password', {
            method: 'POST',
            body: JSON.stringify({ Email: email })
        });
        return response.json();
    }

    async verifyResetCode(email, code) {
        const response = await this.request('/auth/verify-reset-code', {
            method: 'POST',
            body: JSON.stringify({ Email: email, Code: code })
        });
        return response.json();
    }

    async resetPassword(email, code, newPassword, confirmPassword) {
        const response = await this.request('/auth/reset-password', {
            method: 'POST',
            body: JSON.stringify({ 
                Email: email,
                Code: code,
                NewPassword: newPassword,
                ConfirmPassword: confirmPassword
            })
        });
        return response.json();
    }

    async validateToken() {
        const response = await this.request('/auth/validate-token', {
            method: 'POST'
        });
        return response.json();
    }

    async getCourses() {
        const response = await this.request('/courses');
        return response.json();
    }

    async getCourse(courseId) {
        const response = await this.request(`/courses/${courseId}`);
        return response.json();
    }

    async getCourseModules(courseId) {
        const response = await this.request(`/courses/${courseId}/modules`);
        return response.json();
    }

    async getModuleLessons(moduleId) {
        const response = await this.request(`/courses/modules/${moduleId}/lessons`);
        return response.json();
    }

    async getLesson(lessonId) {
        const response = await this.request(`/courses/lessons/${lessonId}`);
        return response.json();
    }

    async getCodeTemplate(lessonId, languageId) {
        const response = await this.request(`/courses/lessons/${lessonId}/code-template/${languageId}`);
        return response.json();
    }

    async getQuizQuestions(lessonId) {
        const response = await this.request(`/quiz/lessons/${lessonId}/questions`);
        return response.json();
    }

    async submitQuiz(answers) {
        const response = await this.request('/quiz/submit-quiz', {
            method: 'POST',
            body: JSON.stringify({ answers })
        });
        return response.json();
    }

    async checkAnswer(questionId, userAnswer) {
        const response = await this.request('/quiz/check-answer', {
            method: 'POST',
            body: JSON.stringify({ questionId, userAnswer })
        });
        return response.json();
    }

    async getAdminUsers() {
        const response = await this.request('/admin/users');
        return response.json();
    }

    async updateUserRole(userId, role) {
        const response = await this.request(`/admin/users/${userId}/role`, {
            method: 'PUT',
            body: JSON.stringify({ role })
        });
        return response.json();
    }

    async getAdminStatistics() {
        const response = await this.request('/admin/statistics');
        return response.json();
    }

    async createCourse(courseData) {
        const response = await this.request('/admin/courses', {
            method: 'POST',
            body: JSON.stringify(courseData)
        });
        return response.json();
    }

async deleteUser(userId) {
    try {
        const response = await this.request(`/admin/users/${userId}`, {
            method: 'DELETE'
        });
        
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }
        
        return await response.json();
    } catch (error) {
        console.error('Delete user error:', error);
        return { success: false, error: error.message };
    }
}

async updateUser(userId, userData) {
    const response = await this.request(`/admin/users/${userId}`, {
        method: 'PUT',
        body: JSON.stringify(userData)
    });
    return response.json();
}

async changePassword(passwordData) {
    const response = await this.request('/auth/change-password', {
        method: 'POST',
        body: JSON.stringify(passwordData)
    });
    return response.json();
}

async updateUserPassword(userId, passwordData) {
    const response = await this.request(`/admin/users/${userId}/password`, {
        method: 'PUT',
        body: JSON.stringify(passwordData)
    });
    return response.json();
}

async revokeUserSessions(userId) {
    try {
        const response = await this.request(`/admin/users/${userId}/revoke-sessions`, {
            method: 'POST'
        });
        
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }
        
        return await response.json();
    } catch (error) {
        console.error('Revoke sessions error:', error);
        return { success: false, error: error.message };
    }
}

    async createUser(userData) {
        try {
            const response = await this.request('/admin/users', {
                method: 'POST',
                body: JSON.stringify(userData)
            });
            
            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }
            
            return await response.json();
        } catch (error) {
            console.error('Create user API error:', error);
            return { success: false, error: error.message };
        }
    }

    async initializeRoles() {
        const response = await this.request('/admin/init-roles', {
            method: 'POST'
        });
        return response.json();
    }

    async checkRoles() {
        const response = await this.request('/admin/check-roles');
        return response.json();
    }

    async checkUserRoles() {
        const response = await this.request('/admin/check-user-roles');
        return response.json();
    }

    async addTestUserRole() {
        const response = await this.request('/admin/add-test-user-role', {
            method: 'POST'
        });
        return response.json();
    }

    async cleanupExpiredTokens() {
        const response = await this.request('/admin/cleanup-expired-tokens', {
            method: 'POST'
        });
        return response.json();
    }
}