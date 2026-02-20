import { ApiService } from '../services/ApiService.js';
import { UIManager } from './UIManager.js';

export class TeacherPanel {
    constructor() {
        this.api = new ApiService();
        this.uiManager = new UIManager();
        this.currentView = 'dashboard';
        this.currentCourseId = null;
        this.currentStudentId = null;
    }

    async initialize() {
        this.setupEventListeners();
        await this.loadDashboard();
    }

    setupEventListeners() {
        document.querySelectorAll('.teacher-nav-item').forEach(btn => {
            btn.addEventListener('click', (e) => {
                const view = e.target.closest('.teacher-nav-item').dataset.view;
                this.switchView(view);
            });
        });

        document.getElementById('back-to-main')?.addEventListener('click', () => {
            window.location.href = '/';
        });

        document.getElementById('create-course-btn')?.addEventListener('click', () => {
            document.getElementById('create-course-modal').classList.remove('hidden');
        });

        document.getElementById('create-course-form')?.addEventListener('submit', (e) => {
            e.preventDefault();
            this.createCourse();
        });

        document.getElementById('course-filter')?.addEventListener('change', (e) => {
            this.filterStudentsByCourse(e.target.value);
        });
    }

    switchView(view) {
        this.currentView = view;
        
        document.querySelectorAll('.teacher-view').forEach(el => {
            el.classList.add('hidden');
        });
        
        document.getElementById(`${view}-view`).classList.remove('hidden');
        
        document.querySelectorAll('.teacher-nav-item').forEach(btn => {
            btn.classList.remove('active');
        });
        document.querySelector(`[data-view="${view}"]`).classList.add('active');
        
        switch(view) {
            case 'dashboard':
                this.loadDashboard();
                break;
            case 'courses':
                this.loadCourses();
                break;
            case 'students':
                this.loadStudents();
                break;
        }
    }

    async loadDashboard() {
        try {
            const result = await this.api.getTeacherDashboard();
            if (result.success) {
                this.renderDashboard(result.dashboard);
            }
        } catch (error) {
            console.error('Ошибка загрузки дашборда:', error);
        }
    }

    renderDashboard(dashboard) {
        document.getElementById('total-students').textContent = dashboard.totalStudents || 0;
        document.getElementById('active-courses').textContent = dashboard.activeCourses || 0;
        document.getElementById('completed-lessons').textContent = dashboard.totalLessonsCompleted || 0;

        const container = document.getElementById('dashboard-courses-list');
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
                    <button class="btn-secondary btn-sm" onclick="window.teacherPanel.viewCourseStudents('${course.id}')">
                        👥 Студенты
                    </button>
                    <button class="btn-primary btn-sm" onclick="window.teacherPanel.manageCourse('${course.id}')">
                        ⚙️ Управлять
                    </button>
                </div>
            </div>
        `).join('');
    }

    async loadCourses() {
        const result = await this.api.getTeacherDashboard();
        if (result.success) {
            this.renderCoursesList(result.dashboard.courses);
        }
    }

    renderCoursesList(courses) {
        const container = document.getElementById('courses-list');
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
                    <button class="btn-secondary btn-sm" onclick="window.teacherPanel.editCourse('${course.id}')">
                        ✏️ Редактировать
                    </button>
                    <button class="btn-secondary btn-sm" onclick="window.teacherPanel.manageLessons('${course.id}')">
                        📖 Уроки
                    </button>
                </div>
            </div>
        `).join('');
    }

    async loadStudents() {
        try {
            const dashboard = await this.api.getTeacherDashboard();
            if (!dashboard.success) return;

            // Заполняем фильтр курсов
            const filter = document.getElementById('course-filter');
            filter.innerHTML = '<option value="">Все курсы</option>' + 
                dashboard.dashboard.courses.map(c => 
                    `<option value="${c.id}">${c.title}</option>`
                ).join('');

            // Если есть курсы, загружаем студентов первого курса
            if (dashboard.dashboard.courses.length > 0) {
                await this.loadCourseStudents(dashboard.dashboard.courses[0].id);
            }
        } catch (error) {
            console.error('Ошибка загрузки студентов:', error);
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
        const container = document.getElementById('students-list');
        
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
                                <button class="btn-secondary btn-xs" onclick="window.teacherPanel.viewStudentProgress('${student.userId}')">
                                    👁️ Детали
                                </button>
                            </td>
                        </tr>
                    `).join('')}
                </tbody>
            </table>
        `;
    }

    filterStudentsByCourse(courseId) {
        if (courseId) {
            this.loadCourseStudents(courseId);
        }
    }

    async viewCourseStudents(courseId) {
        this.currentCourseId = courseId;
        this.switchView('students');
        await this.loadCourseStudents(courseId);
    }

    async viewStudentProgress(studentId) {
        if (!this.currentCourseId) return;
        
        try {
            const result = await this.api.getStudentProgress(studentId, this.currentCourseId);
            if (result.success) {
                this.showStudentProgressModal(result.progress);
            }
        } catch (error) {
            console.error('Ошибка загрузки прогресса студента:', error);
        }
    }

    showStudentProgressModal(progress) {
        const modal = document.getElementById('student-progress-modal');
        document.getElementById('student-name').textContent = progress.username;
        document.getElementById('student-email').textContent = progress.email;
        document.getElementById('student-enrolled').textContent = new Date(progress.enrolledAt).toLocaleDateString();
        document.getElementById('student-total-progress').textContent = progress.courseProgress;

        // Отображаем модули и уроки
        const container = document.getElementById('modules-progress');
        container.innerHTML = progress.modules.map(module => `
            <div class="module-progress">
                <h4>${module.moduleTitle}</h4>
                <div class="lessons-list">
                    ${module.lessons.map(lesson => `
                        <div class="lesson-progress-item">
                            <div style="display: flex; justify-content: space-between; align-items: center;">
                                <div>
                                    <strong>${lesson.lessonTitle}</strong>
                                    <span class="status-badge ${lesson.isCompleted ? 'completed' : 'pending'}">
                                        ${lesson.isCompleted ? '✅ Завершен' : '⏳ В процессе'}
                                    </span>
                                </div>
                                <div class="action-buttons">
                                    <button class="btn-primary btn-xs" onclick="window.teacherPanel.markLessonCompleted('${progress.userId}', '${lesson.lessonId}')">
                                        ✓ Завершить
                                    </button>
                                    <button class="btn-warning btn-xs" onclick="window.teacherPanel.resetLesson('${progress.userId}', '${lesson.lessonId}')">
                                        ↻ Сбросить
                                    </button>
                                </div>
                            </div>
                            ${lesson.submissions && lesson.submissions.length > 0 ? `
                                <details>
                                    <summary>Последние попытки (${lesson.submissions.length})</summary>
                                    ${lesson.submissions.map(sub => `
                                        <div class="submission-item">
                                            <small>${new Date(sub.createdAt).toLocaleString()}</small>
                                            <span>Тесты: ${sub.testsPassed}/${sub.testsTotal}</span>
                                        </div>
                                    `).join('')}
                                </details>
                            ` : ''}
                        </div>
                    `).join('')}
                </div>
            </div>
        `).join('');

        modal.classList.remove('hidden');
    }

    async markLessonCompleted(studentId, lessonId) {
        if (!confirm('Отметить урок как завершенный для этого студента?')) return;
        
        try {
            const result = await this.api.performTeacherAction({
                userId: studentId,
                lessonId: lessonId,
                action: 'complete'
            });
            
            if (result.success) {
                alert('Урок отмечен как завершенный');
                this.viewStudentProgress(studentId);
            }
        } catch (error) {
            console.error('Ошибка:', error);
        }
    }

    async resetLesson(studentId, lessonId) {
        if (!confirm('Сбросить прогресс урока для этого студента? Это действие нельзя отменить.')) return;
        
        try {
            const result = await this.api.performTeacherAction({
                userId: studentId,
                lessonId: lessonId,
                action: 'reset'
            });
            
            if (result.success) {
                alert('Прогресс урока сброшен');
                this.viewStudentProgress(studentId); 
            }
        } catch (error) {
            console.error('Ошибка:', error);
        }
    }

    async createCourse() {
        const title = document.getElementById('course-title').value;
        const description = document.getElementById('course-description').value;
        const difficulty = document.getElementById('course-difficulty').value;

        if (!title) {
            alert('Введите название курса');
            return;
        }

        try {
            alert('Функция создания курса в разработке');
            closeCreateCourse();
        } catch (error) {
            console.error('Ошибка создания курса:', error);
        }
    }

    editCourse(courseId) {
        alert('Редактирование курса будет доступно позже');
    }

    manageLessons(courseId) {
        alert('Управление уроками будет доступно позже');
    }

    manageCourse(courseId) {
        alert('Управление курсом будет доступно позже');
    }
}

document.addEventListener('DOMContentLoaded', () => {
    window.teacherPanel = new TeacherPanel();
    window.teacherPanel.initialize();
});