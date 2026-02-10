import { ApiService } from '../services/ApiService.js';

export class AdminManager {
    constructor(apiService, uiManager) {
        this.api = apiService;
        this.uiManager = uiManager;
        this.currentView = 'statistics';
        this.currentUsers = []; 
    }

    initialize() {
        this.setupAdminEventListeners();
    }

    setupAdminEventListeners() {
    console.log('Setting up admin event listeners...');
    
    document.getElementById('admin-nav-users')?.addEventListener('click', () => this.showAdminView('users'));
    document.getElementById('admin-nav-courses')?.addEventListener('click', () => this.showAdminView('courses'));
    document.getElementById('admin-nav-statistics')?.addEventListener('click', () => this.showAdminView('statistics'));
    console.log('Admin event listeners setup complete');
}

        showAdminView(view) {
        this.currentView = view;
        
        document.querySelectorAll('.admin-view').forEach(el => {
            el.classList.add('hidden');
        });
        
        const targetView = document.getElementById(`admin-${view}`);
        if (targetView) {
            targetView.classList.remove('hidden');
        }

        document.querySelectorAll('.admin-nav-item').forEach(item => {
            item.classList.remove('active');
        });
        document.getElementById(`admin-nav-${view}`)?.classList.add('active');

        switch(view) {
            case 'users':
                this.loadUsers();
                break;
            case 'statistics':
                this.loadStatistics(); 
                break;
            case 'courses':
                this.loadCoursesManagement();
                break;
        }
    }

    async loadUsers() {
        try {
            const result = await this.api.getAdminUsers();
            if (result.success) {
                this.currentUsers = result.users; 
                this.renderUsers(this.currentUsers);
            }
        } catch (error) {
            console.error('Ошибка загрузки пользователей:', error);
            this.uiManager.showToast('Ошибка загрузки пользователей', 'error');
        }
    }

    async loadStatistics() {
        try {
            const result = await this.api.getAdminStatistics();
            if (result.success) {
                this.renderStatistics(result.statistics);
            }
        } catch (error) {
            console.error('Ошибка загрузки статистики:', error);
            this.uiManager.showToast('Ошибка загрузки статистики', 'error');
        }
    }


    renderUsers(users) {
        const usersContainer = document.getElementById('admin-users');
        if (!usersContainer) return;

        const mainTemplate = document.getElementById('admin-users-template');
        const rowTemplate = document.getElementById('user-row-template');
        
        if (!mainTemplate || !rowTemplate) return;

        let mainHtml = mainTemplate.innerHTML;

        if (users.length === 0) {
            mainHtml = mainHtml.replace('{{content}}', '<p class="muted">Пользователи не найдены</p>');
        } else {
            let tableHtml = `
                <table class="users-table">
                    <thead>
                        <tr>
                            <th>ИМЯ</th>
                            <th>EMAIL</th>
                            <th>ТЕЛЕФОН</th>
                            <th>РОЛЬ</th>
                            <th>ПОСЛЕДНИЙ ВХОД</th>
                            <th>ДЕЙСТВИЯ</th>
                        </tr>
                    </thead>
                    <tbody>
            `;
            
            users.forEach(user => {
                const roleOptions = [
                    { value: 'student', label: 'Студент', selected: user.role === 'student' },
                    { value: 'teacher', label: 'Преподаватель', selected: user.role === 'teacher' },
                    { value: 'admin', label: 'Администратор', selected: user.role === 'admin' }
                ];
                
                let roleSelectHtml = roleOptions.map(option => 
                    `<option value="${option.value}" ${option.selected ? 'selected' : ''}>${option.label}</option>`
                ).join('');
                
                let rowHtml = rowTemplate.innerHTML
                    .replace(/{{id}}/g, user.id)
                    .replace('{{username}}', this.escapeHtml(user.username))
                    .replace('{{email}}', this.escapeHtml(user.email))
                    .replace('{{phone}}', this.escapeHtml(user.phone || 'Не указан'))
                    .replace('{{lastLogin}}', user.lastLogin ? new Date(user.lastLogin).toLocaleDateString('ru-RU') : 'Никогда')
                    .replace('{{roleSelect}}', roleSelectHtml);
                
                tableHtml += rowHtml;
            });

            tableHtml += '</tbody></table>';
            mainHtml = mainHtml.replace('{{content}}', tableHtml);
        }

        usersContainer.innerHTML = mainHtml;
        const addBtn = document.getElementById('add-user-btn');
        console.log('Add user button after render:', addBtn);
        
        if (addBtn) {
            addBtn.addEventListener('click', () => {
                console.log('Add user button clicked!');
                this.showAddUserModal();
            });
        }
        
        this.setupUsersTableHandlers();
        }

    setupUsersTableHandlers() {
        document.querySelectorAll('.role-select').forEach(select => {
            select.addEventListener('change', (e) => {
                const userId = e.target.dataset.userId;
                const newRole = e.target.value;
                this.updateUserRole(userId, newRole);
            });
        });

        document.querySelectorAll('.edit-user-btn').forEach(btn => {
            btn.addEventListener('click', (e) => {
                const userId = e.target.dataset.userId;
                this.showEditUserModal(userId);
            });
        });

        document.querySelectorAll('.delete-user-btn').forEach(btn => {
            btn.addEventListener('click', (e) => {
                const userId = e.target.dataset.userId;
                this.deleteUser(userId);
            });
        });

        document.querySelectorAll('.revoke-sessions-btn').forEach(btn => {
            btn.addEventListener('click', (e) => {
                const userId = e.target.dataset.userId;
                this.revokeUserSessions(userId);
            });
        });
    }

    renderStatistics(stats) {
        const statsContainer = document.getElementById('admin-statistics');
        if (!statsContainer) return;

        const template = document.getElementById('admin-statistics-template');
        let html = template.innerHTML
            .replace('{{totalUsers}}', stats.totalUsers || 0)
            .replace('{{activeToday}}', stats.activeToday || 0)
            .replace('{{newThisWeek}}', stats.newThisWeek || 0);

        const roleDistribution = Object.entries(stats.usersByRole || {}).map(([role, count]) => `
            <div class="stat-item">
                <span>${this.getRoleDisplayName(role)}:</span>
                <strong>${count}</strong>
            </div>
        `).join('');

        html = html.replace('{{roleDistribution}}', roleDistribution);
        statsContainer.innerHTML = html;
    }

    async updateUserRole(userId, newRole) {
        try {
            const result = await this.api.updateUserRole(userId, newRole);
            if (result.success) {
                this.uiManager.showToast(`Роль пользователя изменена на "${this.getRoleDisplayName(newRole)}"`, 'success');
            } else {
                throw new Error(result.error);
            }
        } catch (error) {
            this.uiManager.showToast('Ошибка изменения роли', 'error');
            this.loadUsers();
        }
    }

    async deleteUser(userId) {
        const user = this.currentUsers.find(u => u.id === userId);
        const userName = user ? user.username : 'пользователь';
        
        const confirmDelete = confirm(`Вы уверены, что хотите удалить пользователя "${userName}"? Это действие нельзя отменить.`);
        
        if (!confirmDelete) return;
        
        try {
            this.uiManager.showToast('Удаление пользователя...', 'info');
            
            const result = await this.api.deleteUser(userId);
            
            if (result.success) {
                this.uiManager.showToast(`Пользователь "${userName}" успешно удален`, 'success');
                this.loadUsers();
            } else {
                throw new Error(result.error || 'Неизвестная ошибка');
            }
        } catch (error) {
            console.error('Delete user error:', error);
            this.uiManager.showToast(`Ошибка удаления: ${error.message}`, 'error');
        }
    }

        async revokeUserSessions(userId) {
        const user = this.currentUsers.find(u => u.id === userId);
        const userName = user ? user.username : 'пользователь';
        
        const confirmRevoke = confirm(`Вы уверены, что хотите завершить все сессии пользователя "${userName}"? Пользователь будет НЕМЕДЛЕННО разлогинен со всех устройств.`);
        
        if (!confirmRevoke) return;
        
        try {
            this.uiManager.showToast('Завершение сессий...', 'info');
            
            const result = await this.api.revokeUserSessions(userId);
            
            if (result.success) {
                this.uiManager.showToast(`Все сессии пользователя "${userName}" завершены`, 'success');
            } else {
                throw new Error(result.error || 'Неизвестная ошибка');
            }
        } catch (error) {
            console.error('Revoke sessions error:', error);
            this.uiManager.showToast(`Ошибка завершения сессий: ${error.message}`, 'error');
        }
    }

    showEditUserModal(userId) {
        const user = this.currentUsers.find(u => u.id === userId);
        if (!user) {
            console.error('User not found in current data');
            return;
        }

        if (!document.getElementById('modal-edit-user')) {
            this.createEditUserModal();
        }

        document.getElementById('edit-user-id').value = userId;
        document.getElementById('edit-username').value = user.username;
        document.getElementById('edit-email').value = user.email;
        document.getElementById('edit-phone').value = user.phone === 'Не указан' ? '' : user.phone;

        document.getElementById('modal-edit-user').classList.remove('hidden');
    }

    showChangePasswordModal(userId) {
        const user = this.currentUsers.find(u => u.id === userId);
        if (!user) {
            console.error('Пользователь не найден для смены пароля');
            return;
        }

        if (!document.getElementById('modal-change-password-admin')) {
            this.createChangePasswordModal();
        }

        document.getElementById('change-password-user-id').value = userId;
        document.getElementById('change-password-username').textContent = user.username;
        
        document.getElementById('modal-change-password-admin').classList.remove('hidden');
    }

    createEditUserModal() {
        const modal = document.createElement('div');
        modal.id = 'modal-edit-user';
        modal.className = 'modal hidden';
        
        const template = document.getElementById('modal-edit-user-template');
        modal.innerHTML = template.innerHTML;
        
        document.body.appendChild(modal);
        
        document.getElementById('edit-user-save').addEventListener('click', () => {
            this.saveUserChanges();
        });

        document.getElementById('change-password-btn').addEventListener('click', () => {
            const userId = document.getElementById('edit-user-id').value;
            this.showChangePasswordModal(userId);
        });

        document.getElementById('edit-user-cancel').addEventListener('click', () => {
            this.hideEditUserModal();
        });
    }

    createChangePasswordModal() {
        const modal = document.createElement('div');
        modal.id = 'modal-change-password-admin';
        modal.className = 'modal hidden';
        
        const template = document.getElementById('modal-change-password-admin-template');
        modal.innerHTML = template.innerHTML;
        
        document.body.appendChild(modal);
        
        document.getElementById('change-password-save').addEventListener('click', () => {
            this.savePasswordChanges();
        });

        document.getElementById('change-password-cancel').addEventListener('click', () => {
            this.hideChangePasswordModal();
        });
    }

    async saveUserChanges() {
        const userId = document.getElementById('edit-user-id').value;
        const userData = {
            username: document.getElementById('edit-username').value,
            email: document.getElementById('edit-email').value,
            phone: document.getElementById('edit-phone').value
        };

        try {
            this.uiManager.showButtonLoading('edit-user-save', true);
            
            const result = await this.api.updateUser(userId, userData);
            
            if (result.success) {
                this.uiManager.showToast('Пользователь успешно обновлен', 'success');
                this.hideEditUserModal();
                this.loadUsers();
            } else {
                throw new Error(result.error);
            }
        } catch (error) {
            this.uiManager.showToast(error.message, 'error');
        } finally {
            this.uiManager.showButtonLoading('edit-user-save', false);
        }
    }

    async savePasswordChanges() {
        const userId = document.getElementById('change-password-user-id').value;
        const newPassword = document.getElementById('new-password-admin').value;
        const confirmPassword = document.getElementById('confirm-new-password-admin').value;

        if (newPassword !== confirmPassword) {
            this.uiManager.showToast('Пароли не совпадают', 'error');
            return;
        }

        if (newPassword.length < 6) {
            this.uiManager.showToast('Пароль должен быть не менее 6 символов', 'error');
            return;
        }

        try {
            this.uiManager.showButtonLoading('change-password-save', true);
            
            const result = await this.api.updateUserPassword(userId, {
                newPassword: newPassword,
                confirmPassword: confirmPassword
            });
            
            if (result.success) {
                this.uiManager.showToast('Пароль успешно изменен', 'success');
                this.hideChangePasswordModal();
            } else {
                throw new Error(result.error);
            }
        } catch (error) {
            console.error('Ошибка смены пароля:', error);
            this.uiManager.showToast(error.message, 'error');
        } finally {
            this.uiManager.showButtonLoading('change-password-save', false);
        }
    }

    hideEditUserModal() {
        document.getElementById('modal-edit-user').classList.add('hidden');
        document.getElementById('form-edit-user').reset();
    }

    hideChangePasswordModal() {
        document.getElementById('modal-change-password-admin').classList.add('hidden');
        document.getElementById('form-change-password-admin').reset();
    }

    showAddUserModal() {
        if (!document.getElementById('modal-create-user')) {
            this.createAddUserModal();
        }
        
        document.getElementById('form-create-user').reset();
        document.getElementById('modal-create-user').classList.remove('hidden');
    }

    createAddUserModal() {
        const modal = document.createElement('div');
        modal.id = 'modal-create-user';
        modal.className = 'modal hidden';
        
        const template = document.getElementById('modal-create-user-template');
        modal.innerHTML = template.innerHTML;
        
        document.body.appendChild(modal);
        
        document.getElementById('create-user-save').addEventListener('click', () => {
            this.createUser();
        });

        document.getElementById('create-user-cancel').addEventListener('click', () => {
            this.hideAddUserModal();
        });

        const passwordInput = document.getElementById('create-password');
        const strengthBar = document.getElementById('create-password-strength-bar');
        if (passwordInput && strengthBar) {
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
                    strengthBar.style.background = "red";
                } else if (strength === 2 || strength === 3) {
                    strengthBar.style.background = "orange";
                } else if (strength === 4) {
                    strengthBar.style.background = "green";
                }
            });
        }
    }

        async createUser() {
        const username = document.getElementById('create-username').value;
        const email = document.getElementById('create-email').value;
        const phone = document.getElementById('create-phone').value;
        const password = document.getElementById('create-password').value;
        const role = document.getElementById('create-role').value;

        if (!username || !email || !password) {
            this.uiManager.showToast('Заполните все обязательные поля', 'error');
            return;
        }

        if (password.length < 6) {
            this.uiManager.showToast('Пароль должен быть не менее 6 символов', 'error');
            return;
        }

        try {
            this.uiManager.showButtonLoading('create-user-save', true);
            
            const userData = {
                username: username,
                email: email,
                phone: phone,
                password: password,
                role: role
            };

            console.log('Sending user data:', userData);

            const result = await this.api.createUser(userData);
            
            if (result.success) {
                this.uiManager.showToast('Пользователь успешно создан', 'success');
                this.hideAddUserModal();
                this.loadUsers();
            } else {
                throw new Error(result.error || 'Неизвестная ошибка сервера');
            }
        } catch (error) {
            console.error('Ошибка создания пользователя:', error);
            console.log('Response details:', error.response); 
            this.uiManager.showToast(`Ошибка создания пользователя: ${error.message}`, 'error');
        } finally {
            this.uiManager.showButtonLoading('create-user-save', false);
        }
    }

    hideAddUserModal() {
        const modal = document.getElementById('modal-create-user');
        if (modal) {
            modal.classList.add('hidden');
        }
        document.getElementById('form-create-user').reset();
    }

    loadCoursesManagement() {
        const coursesContainer = document.getElementById('admin-courses');
        if (!coursesContainer) return;

        const template = document.getElementById('admin-courses-template');
        coursesContainer.innerHTML = template.innerHTML;
    }

    async createCourse() {
        this.uiManager.showToast('Функция создания курса в разработке', 'info');
    }

    escapeHtml(unsafe) {
        return unsafe
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }

    getRoleDisplayName(role) {
        const roles = {
            'student': 'Студент',
            'teacher': 'Преподаватель',
            'admin': 'Администратор'
        };
        return roles[role] || role;
    }
}