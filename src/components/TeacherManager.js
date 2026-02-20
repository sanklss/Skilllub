import { ApiService } from '../services/ApiService.js';

export class TeacherManager {
    constructor(apiService, uiManager) {
        this.api = apiService;
        this.uiManager = uiManager;
        this.currentView = 'dashboard';
        this.currentCourseId = null;
        this.currentStudentId = null;
        this.courses = [];
        this.students = [];
    }

    async initialize() {
        this.setupTeacherEventListeners();
        await this.loadDashboard();
    }

    setupTeacherEventListeners() {
        document.getElementById('teacher-nav-dashboard')?.addEventListener('click', () => this.showTeacherView('dashboard'));
        document.getElementById('teacher-nav-courses')?.addEventListener('click', () => this.showTeacherView('courses'));
        document.getElementById('teacher-nav-students')?.addEventListener('click', () => this.showTeacherView('students'));
        document.getElementById('teacher-nav-statistics')?.addEventListener('click', () => this.showTeacherView('statistics'));
        
        document.getElementById('create-course-btn')?.addEventListener('click', () => {
            this.uiManager.showToast('Функция создания курса в разработке', 'info');
        });
    }

    async showTeacherView(view) {
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
            case 'dashboard':
                await this.loadDashboard();
                break;
            case 'courses':
                await this.loadTeacherCourses();
                break;
            case 'students':
                await this.loadTeacherStudents();
                break;
            case 'statistics':
                await this.loadTeacherStatistics();
                break;
        }
    }

    async loadDashboard() {
    try {
        this.uiManager.showLoading(true); 
        const result = await this.api.getTeacherDashboard();
        
        if (result.success) {
            this.renderDashboard(result.dashboard);
        } else {
            this.uiManager.showToast('Ошибка загрузки дашборда', 'error');
        }
    } catch (error) {
        console.error('Ошибка загрузки дашборда:', error);
        this.uiManager.showToast('Ошибка загрузки дашборда', 'error');
    } finally {
        this.uiManager.showLoading(false); 
    }
}

    renderDashboard(dashboard) {
        const totalStudentsEl = document.getElementById('dashboard-total-students');
        if (totalStudentsEl) totalStudentsEl.textContent = dashboard.totalStudents || 0;
        
        const activeCoursesEl = document.getElementById('dashboard-active-courses');
        if (activeCoursesEl) activeCoursesEl.textContent = dashboard.activeCourses || 0;
        
        const completedLessonsEl = document.getElementById('dashboard-completed-lessons');
        if (completedLessonsEl) completedLessonsEl.textContent = dashboard.totalLessonsCompleted || 0;

        // Отображаем список курсов
        const container = document.getElementById('dashboard-courses-list');
        if (!container) return;

        if (!dashboard.courses || dashboard.courses.length === 0) {
            container.innerHTML = '<p class="muted">У вас пока нет курсов</p>';
            return;
        }

        container.innerHTML = dashboard.courses.map(course => `
            <div class="course-card teacher-course-card" data-course-id="${course.id}">
                <h3>${course.title}</h3>
                <p class="description">${course.description || 'Нет описания'}</p>
                <div class="course-stats">
                    <div class="stat">
                        <span class="stat-value">${course.studentCount}</span>
                        <span class="stat-label">студентов</span>
                    </div>
                    <div class="stat">
                        <span class="stat-value">${course.modulesCount}</span>
                        <span class="stat-label">модулей</span>
                    </div>
                    <div class="stat">
                        <span class="stat-value">${course.lessonsCount}</span>
                        <span class="stat-label">уроков</span>
                    </div>
                </div>
                <div class="progress-info">
                    <span>Средний прогресс: ${Math.round(course.averageProgress)}%</span>
                    <div class="progress-bar-small">
                        <div class="progress-fill" style="width: ${course.averageProgress}%"></div>
                    </div>
                </div>
                <div class="course-actions">
                    <button class="btn-secondary btn-sm" onclick="app.teacherManager.viewCourseStudents('${course.id}')">
                        👥 Студенты
                    </button>
                    <button class="btn-primary btn-sm" onclick="app.teacherManager.editCourse('${course.id}')">
                        ⚙️ Управлять
                    </button>
                </div>
            </div>
        `).join('');
    }

    async loadTeacherCourses() {
        try {
            const result = await this.api.getTeacherDashboard();
            if (result.success) {
                this.renderCoursesList(result.dashboard.courses);
            }
        } catch (error) {
            console.error('Ошибка загрузки курсов:', error);
            this.uiManager.showToast('Ошибка загрузки курсов', 'error');
        }
    }

    renderCoursesList(courses) {
        const container = document.getElementById('teacher-courses-list');
        if (!container) return;

        if (!courses || courses.length === 0) {
            container.innerHTML = '<p class="muted">У вас пока нет созданных курсов</p>';
            return;
        }

        container.innerHTML = courses.map(course => `
            <div class="course-card teacher-course-card">
                <h3>${course.title}</h3>
                <p class="description">${course.description || 'Нет описания'}</p>
                <div class="course-meta">
                    <span>📊 Прогресс: ${Math.round(course.averageProgress)}%</span>
                    <span>👥 ${course.studentCount} студентов</span>
                </div>
                <div class="course-actions">
                    <button class="btn-secondary btn-sm" onclick="app.teacherManager.editCourse('${course.id}')">
                        ✏️ Редактировать
                    </button>
                    <button class="btn-secondary btn-sm" onclick="app.teacherManager.manageLessons('${course.id}')">
                        📖 Уроки
                    </button>
                </div>
            </div>
        `).join('');
    }

    async loadTeacherStudents() {
        try {
            const dashboard = await this.api.getTeacherDashboard();
            if (!dashboard.success) return;

            // Заполняем фильтр курсов
            const filter = document.getElementById('student-course-filter');
            if (filter) {
                filter.innerHTML = '<option value="">Все курсы</option>' + 
                    (dashboard.dashboard.courses || []).map(c => 
                        `<option value="${c.id}">${c.title}</option>`
                    ).join('');
            }

            if (dashboard.dashboard.courses && dashboard.dashboard.courses.length > 0) {
                await this.loadCourseStudents(dashboard.dashboard.courses[0].id);
            } else {
                const container = document.getElementById('teacher-students-list');
                if (container) {
                    container.innerHTML = '<p class="muted">У вас пока нет студентов</p>';
                }
            }
        } catch (error) {
            console.error('Ошибка загрузки студентов:', error);
            this.uiManager.showToast('Ошибка загрузки студентов', 'error');
        }
    }

    async loadCourseStudents(courseId) {
        try {
            const result = await this.api.getCourseStudents(courseId);
            if (result.success) {
                this.renderStudentsList(result.students);
            }
        } catch (error) {
            console.error('Ошибка загрузки студентов курса:', error);
        }
    }

    renderStudentsList(students) {
        const container = document.getElementById('teacher-students-list');
        if (!container) return;

        if (!students || students.length === 0) {
            container.innerHTML = '<p class="muted">На этом курсе пока нет студентов</p>';
            return;
        }

        container.innerHTML = `
            <table class="students-table">
                <thead>
                    <tr>
                        <th>Имя</th>
                        <th>Email</th>
                        <th>Прогресс</th>
                        <th>Записан</th>
                        <th>Действия</th>
                    </tr>
                </thead>
                <tbody>
                    ${students.map(student => `
                        <tr>
                            <td>${student.username}</td>
                            <td>${student.email}</td>
                            <td>
                                <div style="display: flex; align-items: center;">
                                    <div class="progress-bar-small">
                                        <div class="progress-fill" style="width: ${student.courseProgress}%"></div>
                                    </div>
                                    <span>${student.courseProgress}%</span>
                                </div>
                            </td>
                            <td>${new Date(student.enrolledAt).toLocaleDateString()}</td>
                            <td>
                                <button class="btn-secondary btn-xs" onclick="app.teacherManager.viewStudentProgress('${student.userId}')">
                                    👁️ Детали
                                </button>
                            </td>
                        </tr>
                    `).join('')}
                </tbody>
            </table>
        `;
    }

    async loadTeacherStatistics() {
        try {
            const result = await this.api.getTeacherDashboard();
            if (result.success) {
                this.renderTeacherStatistics(result.dashboard);
            }
        } catch (error) {
            console.error('Ошибка загрузки статистики:', error);
            this.uiManager.showToast('Ошибка загрузки статистики', 'error');
        }
    }

    renderTeacherStatistics(dashboard) {
        const totalStudentsEl = document.getElementById('stats-total-students');
        if (totalStudentsEl) totalStudentsEl.textContent = dashboard.totalStudents || 0;
        
        const avgProgressEl = document.getElementById('stats-average-progress');
        if (avgProgressEl) avgProgressEl.textContent = dashboard.averageProgress || 0 + '%';

        const popularCoursesEl = document.getElementById('stats-popular-courses');
        if (popularCoursesEl) {
            if (!dashboard.courses || dashboard.courses.length === 0) {
                popularCoursesEl.innerHTML = '<p class="muted">Статистика по курсам появится после их создания</p>';
            } else {
                popularCoursesEl.innerHTML = dashboard.courses.map(c => `
                    <div class="stat-item">
                        <span>${c.title}:</span>
                        <strong>${c.studentCount} студентов, ${Math.round(c.averageProgress)}%</strong>
                    </div>
                `).join('');
            }
        }
    }

    async viewCourseStudents(courseId) {
        this.currentCourseId = courseId;
        await this.showTeacherView('students');
        await this.loadCourseStudents(courseId);
    }

    async viewStudentProgress(studentId) {
        this.currentStudentId = studentId;
        this.uiManager.showToast('Детальный прогресс студента будет доступен позже', 'info');
    }

    editCourse(courseId) {
        this.uiManager.showToast('Редактирование курса в разработке', 'info');
    }

    manageLessons(courseId) {
        this.uiManager.showToast('Управление уроками в разработке', 'info');
    }
}