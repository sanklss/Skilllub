import { Validator } from './components/Validator.js';
import { ErrorManager } from './components/ErrorManager.js';
import { ApiService } from './services/ApiService.js';
import { AuthManager } from './components/AuthManager.js';
import { CourseManager } from './components/CourseManager.js';
import { QuizManager } from './components/QuizManager.js';
import { UIManager } from './components/UIManager.js';
import { AdminManager } from './components/AdminManager.js';
import { SessionManager } from './services/SessionManager.js';

class LearnBoxApp {
    constructor() {
        this.validator = new Validator();
        this.errorManager = new ErrorManager();
        this.apiService = new ApiService();
        this.uiManager = new UIManager();
        this.sessionManager = new SessionManager();
        
        this.authManager = new AuthManager(
            this.apiService, 
            this.validator, 
            this.errorManager,
            this.uiManager
        );
        
        this.quizManager = new QuizManager(
            this.apiService,
            this.uiManager
        );
        
        this.courseManager = new CourseManager(
            this.apiService,
            this.uiManager,
            this.quizManager  
        );

        this.adminManager = new AdminManager(
            this.apiService,
            this.uiManager
        );
        
        window.app = this;
    }

    async initialize() {
        console.log('LearnBox App Initializing...');
        
        try {
            this.uiManager.initialize();
            this.initializeValidation();
            await this.authManager.initialize();
            
            if (this.authManager.isAuthenticated()) {
                this.sessionManager.startSessionListener();
            }
            
            this.checkUserRole();
            await this.courseManager.initialize();
            this.quizManager.initialize();
            this.adminManager.initialize();
            this.setupGlobalNavigation();
            
            console.log('LearnBox App Ready');
            
        } catch (error) {
            console.error('Failed to initialize app:', error);
            this.uiManager.showToast('Ошибка инициализации приложения', 'error');
        }
    }

    checkUserRole() {
        const currentUser = this.authManager.getCurrentUser();
        
        document.body.classList.remove('admin-mode', 'teacher-mode');
        
        if (currentUser) {
            if (currentUser.role === 'admin') {
                document.body.classList.add('admin-mode');
                this.uiManager.showSection('admin-panel');
                console.log('Admin mode activated');
            } else if (currentUser.role === 'teacher') {
                document.body.classList.add('teacher-mode');
                this.uiManager.showSection('teacher-panel');
                console.log('Teacher mode activated');
            } else {
                this.uiManager.showSection('catalog');
                console.log('Student mode activated');
            }
        } else {
            this.uiManager.showSection('catalog');
            console.log('Guest mode activated');
        }
        
        this.forceHideNavigation();
    }

    forceHideNavigation() {
        const currentUser = this.authManager.getCurrentUser();
        const headerNav = document.querySelector('.header-nav');
        
        if (headerNav) {
            if (currentUser && (currentUser.role === 'admin' || currentUser.role === 'teacher')) {
                headerNav.style.display = 'none';
                headerNav.style.visibility = 'hidden';
                headerNav.style.opacity = '0';
                headerNav.style.height = '0';
                headerNav.style.overflow = 'hidden';
            } else {
                headerNav.style.display = 'flex';
                headerNav.style.visibility = 'visible';
                headerNav.style.opacity = '1';
                headerNav.style.height = 'auto';
                headerNav.style.overflow = 'visible';
            }
        }
    }

    handleLogin(user) {
        document.body.classList.remove('admin-mode', 'teacher-mode');
        
        if (user.role === 'admin') {
            document.body.classList.add('admin-mode');
            this.showSection('admin-panel');
        } else if (user.role === 'teacher') {
            document.body.classList.add('teacher-mode');
            this.showSection('teacher-panel');
        } else {
            this.showSection('catalog');
        }
        
        this.forceHideNavigation();
    }
 

    initializeValidation() {
        const formIds = [
            'form-login', 'form-signup', 'form-forgot-password',
            'form-reset-code', 'form-new-password', 'change-password-form'
        ];
        
        formIds.forEach(formId => {
            this.errorManager.setupLiveValidation(formId, this.validator);
        });
        
        console.log('Validation initialized');
    }

    setupGlobalNavigation() {
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

    showSection(sectionName) {
        const currentUser = this.authManager.getCurrentUser();
        const isAdmin = currentUser && currentUser.role === 'admin';
        
        if (isAdmin && sectionName !== 'admin-panel') {
            this.uiManager.showSection('admin-panel');
        } else if (!isAdmin && sectionName === 'admin-panel') {
            this.uiManager.showSection('catalog');
        } else {
            this.uiManager.showSection(sectionName);
        }
    }

    openCourse(courseId) {
        this.courseManager.openCourse(courseId);
    }

    openLesson(lessonId) {
        this.courseManager.openLesson(lessonId);
    }
}

document.addEventListener('DOMContentLoaded', () => {
    const app = new LearnBoxApp();
    app.initialize();
});

window.showSection = function(sectionName) {
    if (window.app) {
        window.app.showSection(sectionName);
    }
}

window.openCourse = function(courseId) {
    if (window.app) {
        window.app.openCourse(courseId);
    }
}

window.openLesson = function(lessonId) {
    if (window.app) {
        window.app.openLesson(lessonId);
    }
}

export { LearnBoxApp };