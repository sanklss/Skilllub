import { ApiService } from '../services/ApiService.js';
import { StorageService } from '../services/StorageService.js';

export class AuthManager {
    constructor(apiService, validator, errorManager, uiManager) {
        this.api = apiService;
        this.storage = new StorageService();
        this.validator = validator;
        this.errorManager = errorManager;
        this.uiManager = uiManager;
        this.currentResetEmail = '';
    }

    async initialize() {
        this.setupAuthEventListeners();
        
        if (this.storage.isAuthenticated()) {
            const isValid = await this.validateCurrentToken();
            if (isValid) {
                this.uiManager.showUser(this.storage.getUser());
            } else {
                await this.logout();
            }
        } else {
            this.uiManager.showAuthButtons();
        }
    }
    
    forceLogout() {
        console.log('Force logout triggered');
        
        this.storage.clearAuth();
        
        this.uiManager.showAuthButtons();
        this.uiManager.showSection('catalog');
        this.uiManager.showToast('Вы вышли из системы', 'info');
        
        if (window.app && window.app.sessionManager) {
            window.app.sessionManager.stopSessionListener();
        }
        
        setTimeout(() => {
            window.location.reload();
        }, 1000);
    }

    async validateCurrentToken() {
        try {
            const result = await this.api.validateToken();
            return result.success;
        } catch (error) {
            console.error('Token validation failed:', error);
            return false;
        }
    }

    setupAuthEventListeners() {
        document.getElementById('form-login')?.addEventListener('submit', (e) => {
            e.preventDefault();
            this.handleLogin(e);
        });

        document.getElementById('form-signup')?.addEventListener('submit', (e) => {
            e.preventDefault();
            this.handleSignup(e);
        });

        document.getElementById('form-forgot-password')?.addEventListener('submit', (e) => {
            e.preventDefault();
            this.handleForgotPassword(e);
        });

        document.getElementById('form-reset-code')?.addEventListener('submit', (e) => {
            e.preventDefault();
            this.handleResetCode(e);
        });

        document.getElementById('form-new-password')?.addEventListener('submit', (e) => {
            e.preventDefault();
            this.handleNewPassword(e);
        });

        document.getElementById('change-password-form')?.addEventListener('submit', (e) => {
            e.preventDefault();
            this.handleChangePassword(e);
        });

        document.getElementById('to-signup')?.addEventListener('click', (e) => {
            e.preventDefault();
            this.uiManager.showModal('modal-signup');
            this.uiManager.hideModal('modal-login');
        });

        document.getElementById('to-login')?.addEventListener('click', (e) => {
            e.preventDefault();
            this.uiManager.showModal('modal-login');
            this.uiManager.hideModal('modal-signup');
        });

        document.getElementById('show-forgot-password')?.addEventListener('click', (e) => {
            e.preventDefault();
            this.uiManager.showModal('modal-forgot-password');
            this.uiManager.hideModal('modal-login');
        });
    }    

    async handleLogin(e) {
        this.errorManager.clearAllErrors();
        
        const formData = this.getFormData('form-login');
        const validation = this.validator.validateForm('form-login', formData);
        
        if (!validation.isValid) {
            this.errorManager.showFormErrors(validation.errors);
            this.uiManager.showToast('Исправьте ошибки в форме', 'error');
            return;
        }

        try {
            this.uiManager.showButtonLoading('login-ok', true);
            
            const result = await this.api.login(formData['login-email'], formData['login-password']);
            
            if (result.success) {
                this.storage.setAuthToken(result.token);
                this.storage.setRefreshToken(result.refreshToken);
                this.storage.setUser(result.user);
                
                this.uiManager.showToast(result.message, 'success');
                this.uiManager.hideModal('modal-login');
                this.uiManager.showUser(result.user);

                location.reload();

            } else {
                throw new Error(result.error);
            }
        } catch (error) {
            this.uiManager.showToast(error.message, 'error');
        } finally {
            this.uiManager.showButtonLoading('login-ok', false);
        }
    }

    async handleSignup(e) {
    this.errorManager.clearAllErrors();
    
    const formData = this.getFormData('form-signup');
    const validation = this.validator.validateForm('form-signup', formData);
    
    if (!validation.isValid) {
        this.errorManager.showFormErrors(validation.errors);
        this.uiManager.showToast('Исправьте ошибки в форме', 'error');
        return;
    }

    try {
        this.uiManager.showButtonLoading('signup-ok', true);
        
        const roleSelect = document.getElementById('signup-role');
        const selectedRole = roleSelect ? roleSelect.value : 'student';
        
        const userData = {
            Username: formData['signup-username'],
            Email: formData['signup-email'],
            Phone: formData['signup-phone'],
            Password: formData['signup-password'],
            Role: selectedRole 
        };

        const result = await this.api.register(userData);
        
        if (result.success) {
            this.storage.setAuthToken(result.token);
            this.storage.setRefreshToken(result.refreshToken);
            this.storage.setUser(result.user);
            
            const roleText = result.user.role === 'teacher' ? 'Преподаватель' : 'Студент';
            this.uiManager.showToast(`Регистрация успешна, ${result.user.username}! Ваша роль: ${roleText}`, 'success');
            this.uiManager.hideModal('modal-signup');
            this.uiManager.showUser(result.user);

            location.reload();
        } else {
            throw new Error(result.error);
        }
    } catch (error) {
        this.uiManager.showToast(error.message, 'error');
    } finally {
        this.uiManager.showButtonLoading('signup-ok', false);
    }
}

    async handleForgotPassword(e) {
        this.errorManager.clearAllErrors();
        
        const formData = this.getFormData('form-forgot-password');
        const validation = this.validator.validateForm('form-forgot-password', formData);
        
        if (!validation.isValid) {
            this.errorManager.showFormErrors(validation.errors);
            this.uiManager.showToast('Исправьте ошибки в форме', 'error');
            return;
        }

        try {
            this.uiManager.showButtonLoading('forgot-ok', true);
            
            const result = await this.api.forgotPassword(formData['forgot-email']);
            
            if (result.success) {
                this.currentResetEmail = formData['forgot-email'];
                this.uiManager.showToast('Код отправлен на вашу почту!', 'success');
                this.uiManager.hideModal('modal-forgot-password');
                this.uiManager.showModal('modal-reset-code');
            } else {
                throw new Error(result.error);
            }
        } catch (error) {
            this.uiManager.showToast(error.message, 'error');
        } finally {
            this.uiManager.showButtonLoading('forgot-ok', false);
        }
    }

    async handleResetCode(e) {
        this.errorManager.clearAllErrors();
        
        const formData = this.getFormData('form-reset-code');
        const validation = this.validator.validateForm('form-reset-code', formData);
        
        if (!validation.isValid) {
            this.errorManager.showFormErrors(validation.errors);
            this.uiManager.showToast('Исправьте ошибки в форме', 'error');
            return;
        }

        try {
            this.uiManager.showButtonLoading('reset-code-ok', true);
            
            const code = formData['reset-code'];
            
            const result = await this.api.verifyResetCode(this.currentResetEmail, code);
            
            if (result.success) {
                document.getElementById('form-new-password').dataset.code = code;
                this.uiManager.showToast('Код верный!', 'success');
                this.uiManager.hideModal('modal-reset-code');
                this.uiManager.showModal('modal-new-password');
            } else {
                throw new Error(result.error);
            }
        } catch (error) {
            this.uiManager.showToast(error.message, 'error');
        } finally {
            this.uiManager.showButtonLoading('reset-code-ok', false);
        }
    }

    async handleNewPassword(e) {
        this.errorManager.clearAllErrors();
        
        const formData = this.getFormData('form-new-password');
        const validation = this.validator.validateForm('form-new-password', formData);
        
        if (!validation.isValid) {
            this.errorManager.showFormErrors(validation.errors);
            this.uiManager.showToast('Исправьте ошибки в форме', 'error');
            return;
        }

        const code = document.getElementById('form-new-password').dataset.code;
        await this.resetPassword(this.currentResetEmail, code, formData['new-password']);
    }

    async handleChangePassword(e) {
        e.preventDefault();
        this.errorManager.clearAllErrors();
        
        const formData = this.getFormData('change-password-form');
        const validation = this.validator.validateForm('change-password-form', formData);
        
        if (!validation.isValid) {
            this.errorManager.showFormErrors(validation.errors);
            this.uiManager.showToast('Исправьте ошибки в форме', 'error');
            return;
        }

        try {
            this.uiManager.showButtonLoading('change-password-ok', true);
            
            const result = await this.api.changePassword({
                currentPassword: formData['current-password'],
                newPassword: formData['change-new-password'], 
                confirmPassword: formData['change-confirm-password']
            });
            
            if (result.success) {
                this.uiManager.showToast('Пароль успешно изменён!', 'success');
                this.uiManager.hideModal('modal-change-password');
            } else {
                throw new Error(result.error);
            }
        } catch (error) {
            this.uiManager.showToast(error.message, 'error');
        } finally {
            this.uiManager.showButtonLoading('change-password-ok', false);
        }
    }

    async resetPassword(email, code, newPassword) {
        try {
            this.uiManager.showButtonLoading('new-password-ok', true);
            
            const result = await this.api.resetPassword(email, code, newPassword, newPassword);
            
            if (result.success) {
                this.uiManager.showToast('Пароль успешно изменен! Теперь вы можете войти с новым паролем.', 'success');
                this.uiManager.hideModal('modal-new-password');
                
                setTimeout(() => {
                    this.tryLoginAfterReset(email, newPassword);
                }, 1000);
            } else {
                throw new Error(result.error);
            }
        } catch (error) {
            this.uiManager.showToast(error.message, 'error');
        } finally {
            this.uiManager.showButtonLoading('new-password-ok', false);
        }
    }

    async tryLoginAfterReset(email, password) {
        try {
            const result = await this.api.login(email, password);
            
            if (result.success) {
                this.storage.setAuthToken(result.token);
                this.storage.setRefreshToken(result.refreshToken);
                this.storage.setUser(result.user);
                
                this.uiManager.showToast('Автоматический вход успешен! Пароль действительно изменился.', 'success');
                this.uiManager.showUser(result.user);
            } else {
                this.uiManager.showToast('Пароль изменился, но автоматический вход не удался', 'warning');
            }
        } catch (error) {
            this.uiManager.showToast('Ошибка при автоматическом входе', 'error');
        }
    }

    async logout() {
        this.storage.clearAuth();
        this.uiManager.showAuthButtons();
        this.uiManager.showSection('catalog');
        this.uiManager.showToast('Вы вышли из системы', 'info');

        location.reload();
    }

    getFormData(formId) {
        const form = document.getElementById(formId);
        const data = {};
        
        if (form) {
            const inputs = form.querySelectorAll('input, select, textarea');
            inputs.forEach(input => {
                if (input.name || input.id) {
                    const key = input.name || input.id;
                    data[key] = input.value;
                }
            });
        }
        
        return data;
    }

    getCurrentUser() {
        return this.storage.getUser();
    }

    isAuthenticated() {
        return this.storage.isAuthenticated();
    }
}