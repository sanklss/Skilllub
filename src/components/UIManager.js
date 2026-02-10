export class UIManager {
    constructor() {
        this.modals = [
            'modal-login', 'modal-signup', 'modal-forgot-password',
            'modal-reset-code', 'modal-new-password', 'modal-change-password'
        ];
    }

    initialize() {
        this.setupNavigation();
        this.setupModalEventListeners();
        this.setupPasswordStrengthIndicators();
    }

    setupNavigation() {
        document.addEventListener('click', (e) => {
            const navItem = e.target.closest('.nav-item');
            if (navItem) {
                e.preventDefault();
                const sectionName = navItem.getAttribute('data-section');
                if (sectionName) {
                    this.showSection(sectionName);
                }
            }
        });
    }

    setupModalEventListeners() {
        document.getElementById('login-cancel')?.addEventListener('click', () => this.hideModal('modal-login'));
        document.getElementById('signup-cancel')?.addEventListener('click', () => this.hideModal('modal-signup'));
        document.getElementById('forgot-cancel')?.addEventListener('click', () => this.hideModal('modal-forgot-password'));
        document.getElementById('reset-code-cancel')?.addEventListener('click', () => this.hideModal('modal-reset-code'));
        document.getElementById('new-password-cancel')?.addEventListener('click', () => this.hideModal('modal-new-password'));
        document.getElementById('change-password-cancel')?.addEventListener('click', () => this.hideModal('modal-change-password'));

        const roleSelect = document.getElementById("signup-role");
        const teacherFields = document.getElementById("teacher-fields");
        
        if (roleSelect && teacherFields) {
            roleSelect.addEventListener("change", () => {
                teacherFields.style.display = roleSelect.value === "teacher" ? "block" : "none";
            });
        }
    }

    setupPasswordStrengthIndicators() {
        console.log('Setting up password strength indicators...');
        
        const passwordInputs = [
            { input: 'signup-password', bar: 'signup-password-strength-bar' },
            { input: 'new-password', bar: 'new-password-strength-bar' },
            { input: 'change-new-password', bar: 'change-password-strength-bar' }
        ];

        passwordInputs.forEach(({ input, bar }) => {
            const passwordInput = document.getElementById(input);
            const strengthBar = document.getElementById(bar);
            
            if (passwordInput && strengthBar) {
                const strengthContainer = strengthBar.parentElement;
                strengthContainer.style.cssText = `
                    margin-top: 8px;
                    height: 8px;
                    background: #e5e7eb;
                    border-radius: 4px;
                    overflow: hidden;
                    display: none;
                `;
                
                strengthBar.style.cssText = `
                    height: 100%;
                    width: 0%;
                    border-radius: 4px;
                    transition: all 0.3s ease;
                `;
                
                passwordInput.addEventListener("input", () => {
                    const val = passwordInput.value;
                    
                    let strength = 0;
                    if (val.length >= 6) strength += 1;
                    if (/[A-Z]/.test(val)) strength += 1;
                    if (/[0-9]/.test(val)) strength += 1;
                    if (/[\W_]/.test(val)) strength += 1;

                    const width = (strength / 4) * 100;
                    strengthBar.style.width = width + "%";

                    if (strength <= 1) {
                        strengthBar.style.background = "#e74c3c";
                    } else if (strength === 2 || strength === 3) {
                        strengthBar.style.background = "#f39c12";
                    } else if (strength === 4) {
                        strengthBar.style.background = "#27ae60";
                    }
                    
                    if (val.length > 0) {
                        strengthContainer.style.display = 'block';
                    } else {
                        strengthContainer.style.display = 'none';
                    }
                });

                strengthContainer.style.display = 'none';
            }
        });
    }

    showSection(sectionName) {
        document.querySelectorAll('.page-section').forEach(section => {
            section.classList.remove('active');
        });
        document.querySelectorAll('.section').forEach(section => {
            section.classList.remove('active');
        });

        const section = document.getElementById(sectionName);
        if (section) {
            section.classList.add('active');
        }

        document.querySelectorAll('.nav-item').forEach(item => {
            item.classList.remove('active');
        });
        
        const activeNav = document.querySelector(`[data-section="${sectionName}"]`);
        if (activeNav) {
            activeNav.classList.add('active');
        }
    }

    showModal(modalId) {
        this.hideAllModals();
        const modal = document.getElementById(modalId);
        if (modal) {
            modal.classList.remove('hidden');
        }
    }

    hideModal(modalId) {
        const modal = document.getElementById(modalId);
        if (modal) {
            modal.classList.add('hidden');
        }
    }

    hideAllModals() {
        this.modals.forEach(modalId => {
            this.hideModal(modalId);
        });
    }

    showUser(user) {
        const userControls = document.getElementById("user-controls");
        if (!userControls) return;

        const roleDisplay = this.getRoleDisplayName(user.role);
        
        userControls.innerHTML = `
            <div class="user-dropdown">
                <span id="username-display" class="username" style="cursor:pointer;" title="Роль: ${roleDisplay}">
                    ${user.username} (${roleDisplay}) ▾
                </span>
                <div id="user-menu" class="user-menu hidden">
                    ${user.role === 'admin' ? '<button id="btn-admin-panel" class="ghost">Админ-панель</button>' : ''}
                    ${user.role === 'teacher' ? '<button id="btn-teacher-panel" class="ghost">Панель преподавателя</button>' : ''}
                    <button id="btn-change-password" class="ghost">Сменить пароль</button>
                    <button id="btn-logout" class="ghost">Выйти</button>
                </div>
            </div>
        `;

        const usernameDisplay = document.getElementById("username-display");
        const userMenu = document.getElementById("user-menu");
        const btnLogout = document.getElementById("btn-logout");
        const btnChangePassword = document.getElementById("btn-change-password");
        const btnSettings = document.getElementById("btn-settings");
        const btnAdminPanel = document.getElementById("btn-admin-panel");
        const btnTeacherPanel = document.getElementById("btn-teacher-panel");

        if (usernameDisplay && userMenu) {
            usernameDisplay.addEventListener("click", () => {
                userMenu.classList.toggle("hidden");
            });

            document.addEventListener('click', (e) => {
                if (!userMenu.contains(e.target) && !usernameDisplay.contains(e.target)) {
                    userMenu.classList.add('hidden');
                }
            });
        }

        if (btnLogout) {
            btnLogout.addEventListener("click", () => {
                if (window.app && window.app.authManager) {
                    window.app.authManager.logout();
                }
            });
        }

        if (btnChangePassword) {
            btnChangePassword.addEventListener("click", () => {
                this.showModal('modal-change-password');
                this.clearForm('change-password-form');
            });
        }

        if (btnAdminPanel && user.role === 'admin') {
            btnAdminPanel.addEventListener("click", () => {
                this.showSection('admin-panel');
                userMenu.classList.add('hidden');
            });
        }

        if (btnTeacherPanel && user.role === 'teacher') {
            btnTeacherPanel.addEventListener("click", () => {
                this.showSection('teacher-panel');
                userMenu.classList.add('hidden');
            });
        }

        this.updateProfile(user);
    }

    showAuthButtons() {
        const userControls = document.getElementById("user-controls");
        if (!userControls) return;
        
        userControls.innerHTML = `
            <button id="btn-login" class="btn-login">Войти</button>
            <button id="btn-signup" class="btn-signup">Регистрация</button>
        `;

        this.bindAuthButtons();
    }

    bindAuthButtons() {
        const btnLogin = document.getElementById("btn-login");
        const btnSignup = document.getElementById("btn-signup");

        if (btnLogin) {
            btnLogin.addEventListener("click", () => {
                this.showModal('modal-login');
                this.clearForm('form-login');
            });
        }

        if (btnSignup) {
            btnSignup.addEventListener("click", () => {
                this.showModal('modal-signup');
                this.clearForm('form-signup');
            });
        }
    }

    updateProfile(user) {
        const profileUsername = document.getElementById("profile-username");
        const profileEmail = document.getElementById("profile-email");
        const profileRole = document.getElementById("profile-role");
        
        if (profileUsername) profileUsername.textContent = user.username;
        if (profileEmail) profileEmail.textContent = user.email;
        if (profileRole) {
            const roleText = this.getRoleDisplayName(user.role);
            profileRole.textContent = `Роль: ${roleText}`;
        }
    }

    getRoleDisplayName(role) {
        const roles = {
            'student': 'Студент',
            'teacher': 'Преподаватель',
            'admin': 'Администратор'
        };
        return roles[role] || role;
    }

    showQuizSection() {
        const quizSection = document.getElementById('quiz-section');
        if (quizSection) {
            quizSection.classList.remove('hidden');
        }
    }

    hideQuizSection() {
        const quizSection = document.getElementById('quiz-section');
        if (quizSection) {
            quizSection.classList.add('hidden');
        }
    }

    showCodeSection() {
        const codeSection = document.querySelector('.code-section');
        if (codeSection) {
            codeSection.classList.remove('hidden');
        }
    }

    hideCodeSection() {
        const codeSection = document.querySelector('.code-section');
        if (codeSection) {
            codeSection.classList.add('hidden');
        }
    }

    showToast(message, type = 'info') {
        let container = document.getElementById('toast-container');
        if (!container) {
            container = document.createElement('div');
            container.id = 'toast-container';
            container.className = 'toast-container';
            document.body.appendChild(container);
        }

        const toast = document.createElement('div');
        toast.className = `toast toast-${type}`;
        toast.textContent = message;

        container.appendChild(toast);

        setTimeout(() => {
            toast.remove();
        }, 5000);
    }

    showButtonLoading(buttonId, isLoading) {
        const button = document.getElementById(buttonId);
        if (!button) return;

        if (isLoading) {
            button.disabled = true;
            button.dataset.originalText = button.textContent;
            button.innerHTML = '<div class="button-spinner"></div> Загрузка...';
        } else {
            button.disabled = false;
            button.textContent = button.dataset.originalText || button.textContent;
        }
    }

    clearForm(formId) {
        const form = document.getElementById(formId);
        if (form) {
            form.reset();
        }
    }

    debounce(func, wait) {
        let timeout;
        return function executedFunction(...args) {
            const later = () => {
                clearTimeout(timeout);
                func(...args);
            };
            clearTimeout(timeout);
            timeout = setTimeout(later, wait);
        };
    }
}