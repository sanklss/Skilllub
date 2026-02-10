import { ApiService } from '../services/ApiService.js';

export class TeacherManager {
    constructor(apiService, uiManager) {
        this.api = apiService;
        this.uiManager = uiManager;
        this.currentView = 'courses';
    }

    initialize() {
        this.setupTeacherEventListeners();
        this.hideDashboardButton();
    }

    setupTeacherEventListeners() {
        document.getElementById('teacher-nav-courses')?.addEventListener('click', () => this.showTeacherView('courses'));
        document.getElementById('teacher-nav-students')?.addEventListener('click', () => this.showTeacherView('students'));
        document.getElementById('teacher-nav-statistics')?.addEventListener('click', () => this.showTeacherView('statistics'));
    }

    hideDashboardButton() {
        const dashboardBtn = document.getElementById('teacher-nav-dashboard');
        if (dashboardBtn) {
            dashboardBtn.style.display = 'none';
            dashboardBtn.style.visibility = 'hidden';
            dashboardBtn.style.opacity = '0';
            dashboardBtn.style.height = '0';
            dashboardBtn.style.width = '0';
            dashboardBtn.style.margin = '0';
            dashboardBtn.style.padding = '0';
            dashboardBtn.style.pointerEvents = 'none';
        }
    }

    showTeacherView(view) {
        this.currentView = view;
        
        document.querySelectorAll('.teacher-view').forEach(el => {
            el.classList.add('hidden');
        });
        
        const targetView = document.getElementById(`teacher-${view}`);
        if (targetView) {
            targetView.classList.remove('hidden');
        }

        document.querySelectorAll('.teacher-nav-item').forEach(item => {
            item.classList.remove('active');
        });
        document.getElementById(`teacher-nav-${view}`)?.classList.add('active');

        switch(view) {
            case 'courses':
                this.loadTeacherCourses();
                break;
            case 'students':
                this.loadTeacherStudents();
                break;
            case 'statistics':
                this.loadTeacherStatistics();
                break;
        }
    }

    async loadTeacherCourses() {
        try {
            this.renderTeacherCourses([]);
        } catch (error) {
            console.error('Ошибка загрузки курсов преподавателя:', error);
            this.uiManager.showToast('Ошибка загрузки курсов', 'error');
        }
    }

    async loadTeacherStudents() {
        try {
            this.renderTeacherStudents([]);
        } catch (error) {
            console.error('Ошибка загрузки студентов:', error);
            this.uiManager.showToast('Ошибка загрузки студентов', 'error');
        }
    }

    async loadTeacherStatistics() {
        try {
            this.renderTeacherStatistics({
                totalStudents: 0,
                averageProgress: 0,
                popularCourses: []
            });
        } catch (error) {
            console.error('Ошибка загрузки статистики:', error);
            this.uiManager.showToast('Ошибка загрузки статистики', 'error');
        }
    }

    renderTeacherCourses(courses) {
        const coursesContainer = document.getElementById('teacher-courses');
        if (!coursesContainer) return;

        const template = document.getElementById('teacher-courses-template');
        coursesContainer.innerHTML = template.innerHTML;
    }

    renderTeacherStudents(students) {
        const studentsContainer = document.getElementById('teacher-students');
        if (!studentsContainer) return;

        const template = document.getElementById('teacher-students-template');
        studentsContainer.innerHTML = template.innerHTML;
    }

    renderTeacherStatistics(stats) {
        const statsContainer = document.getElementById('teacher-statistics');
        if (!statsContainer) return;

        const template = document.getElementById('teacher-statistics-template');
        let html = template.innerHTML
            .replace('{{totalStudents}}', stats.totalStudents)
            .replace('{{averageProgress}}', stats.averageProgress);
        
        statsContainer.innerHTML = html;
    }
}