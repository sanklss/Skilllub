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
        document.getElementById('dashboard-total-students').textContent = dashboard.totalStudents || 0;
        document.getElementById('dashboard-active-courses').textContent = dashboard.activeCourses || 0;
        document.getElementById('dashboard-completed-lessons').textContent = dashboard.totalLessonsCompleted || 0;

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

            const filter = document.getElementById('student-course-filter');
            if (filter) {
                filter.innerHTML = '<option value="">Все курсы</option>' + 
                    (dashboard.dashboard.courses || []).map(c => 
                        `<option value="${c.id}">${c.title}</option>`
                    ).join('');
                
                filter.addEventListener('change', (e) => {
                    this.loadCourseStudents(e.target.value);
                });
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
            this.currentCourseId = courseId;
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
        document.getElementById('stats-total-students').textContent = dashboard.totalStudents || 0;
        document.getElementById('stats-average-progress').textContent = (dashboard.averageProgress || 0) + '%';

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
        if (!this.currentCourseId) {
            const dashboard = await this.api.getTeacherDashboard();
            if (dashboard.success && dashboard.dashboard.courses.length > 0) {
                this.currentCourseId = dashboard.dashboard.courses[0].id;
            } else {
                this.uiManager.showToast('Не найден курс для просмотра', 'error');
                return;
            }
        }

        try {
            this.uiManager.showLoading(true);
            const result = await this.api.getStudentProgress(studentId, this.currentCourseId);
            
            if (result.success) {
                this.showStudentProgressModal(result.progress);
            } else {
                this.uiManager.showToast('Ошибка загрузки прогресса', 'error');
            }
        } catch (error) {
            console.error('Ошибка загрузки прогресса:', error);
            this.uiManager.showToast('Ошибка загрузки прогресса', 'error');
        } finally {
            this.uiManager.showLoading(false);
        }
    }

    showStudentProgressModal(progress) {
        let modal = document.getElementById('student-progress-modal');
        
        if (!modal) {
            modal = document.createElement('div');
            modal.id = 'student-progress-modal';
            modal.className = 'modal';
            
            modal.innerHTML = `
                <div class="modal-card" style="max-width: 800px; width: 90%;">
                    <header>
                        <h3>Прогресс студента: <span id="modal-student-name"></span></h3>
                        <button class="close-btn" onclick="document.getElementById('student-progress-modal').classList.add('hidden')">✕</button>
                    </header>
                    <div class="modal-content">
                        <div class="student-info" style="margin-bottom: 20px; padding: 15px; background: #f7fafc; border-radius: 8px;">
                            <p><strong>Email:</strong> <span id="modal-student-email"></span></p>
                            <p><strong>Всего уроков:</strong> <span id="modal-total-lessons">0</span> | 
                               <strong>Пройдено:</strong> <span id="modal-completed-lessons">0</span> | 
                               <strong>Прогресс:</strong> <span id="modal-progress-percent">0%</span></p>
                        </div>
                        
                        <div id="modal-modules-list" class="modules-progress">
                            <!-- Сюда загрузятся модули -->
                        </div>
                    </div>
                </div>
            `;
            
            document.body.appendChild(modal);
        }

        document.getElementById('modal-student-name').textContent = progress.username;
        document.getElementById('modal-student-email').textContent = progress.email;
        document.getElementById('modal-total-lessons').textContent = progress.totalLessons || 0;
        document.getElementById('modal-completed-lessons').textContent = progress.completedLessons || 0;
        
        const percent = progress.totalLessons > 0 
            ? Math.round((progress.completedLessons / progress.totalLessons) * 100) 
            : 0;
        document.getElementById('modal-progress-percent').textContent = percent + '%';

        const modulesContainer = document.getElementById('modal-modules-list');
        
        if (!progress.modules || progress.modules.length === 0) {
            modulesContainer.innerHTML = '<p class="muted">Нет данных о прогрессе</p>';
        } else {
            modulesContainer.innerHTML = progress.modules.map(module => `
                <div class="module-card" style="margin-bottom: 20px; border: 1px solid #e2e8f0; border-radius: 8px; overflow: hidden;">
                    <div class="module-header" style="padding: 12px 15px; background: #edf2f7; cursor: pointer; display: flex; justify-content: space-between; align-items: center;"
                         onclick="this.nextElementSibling.classList.toggle('hidden')">
                        <div>
                            <strong>${module.moduleTitle}</strong>
                            <span style="margin-left: 10px; font-size: 0.9em; color: #718096;">
                                ${module.completedLessons || 0}/${module.totalLessons || 0} уроков
                            </span>
                        </div>
                        <span>${module.isCompleted ? '✅' : '⏳'}</span>
                    </div>
                    <div class="module-lessons hidden" style="padding: 10px;">
                        ${module.lessons.map(lesson => `
                            <div class="lesson-item" style="padding: 10px; margin: 5px 0; background: #f8fafc; border-radius: 4px; border-left: 4px solid ${lesson.isCompleted ? '#48bb78' : '#cbd5e0'};">
                                <div style="display: flex; justify-content: space-between; align-items: center;">
                                    <div>
                                        <strong>${lesson.lessonOrder}. ${lesson.lessonTitle}</strong>
                                        <div style="font-size: 0.9em; color: #4a5568;">
                                            ${lesson.theoryCompleted ? '📖 Теория ✓' : '📖 Теория ✗'}
                                            ${lesson.hasQuiz ? (lesson.quizCompleted ? ' 📝 Квиз ✓' : ' 📝 Квиз ✗') : ''}
                                            ${lesson.hasCodeExercise ? (lesson.codeCompleted ? ' 💻 Код ✓' : ' 💻 Код ✗') : ''}
                                        </div>
                                        ${lesson.bestScore > 0 ? `<div style="font-size: 0.9em; color: #718096;">Лучший результат: ${lesson.bestScore}%</div>` : ''}
                                    </div>
                                    <span style="font-size: 1.2em;">${lesson.isCompleted ? '✅' : '❌'}</span>
                                </div>
                                
                                ${lesson.submissions && lesson.submissions.length > 0 ? `
                                    <details style="margin-top: 8px;">
                                        <summary style="color: #4299e1; cursor: pointer; font-size: 0.9em;">Последние попытки (${lesson.submissions.length})</summary>
                                        <div style="margin-top: 8px; padding: 8px; background: white; border-radius: 4px;">
                                            ${lesson.submissions.map(sub => `
                                                <div style="padding: 5px; border-bottom: 1px solid #e2e8f0; font-size: 0.85em;">
                                                    ${new Date(sub.createdAt).toLocaleString()}
                                                    ${sub.testsTotal ? ` - Тесты: ${sub.testsPassed}/${sub.testsTotal}` : ''}
                                                    ${sub.score ? ` - Оценка: ${sub.score}%` : ''}
                                                </div>
                                            `).join('')}
                                        </div>
                                    </details>
                                ` : ''}
                                
                                <div style="margin-top: 8px; display: flex; gap: 8px;">
                                    <button class="btn-secondary btn-xs" onclick="app.teacherManager.markLessonCompleted('${progress.userId}', '${lesson.lessonId}')">
                                        ✓ Завершить
                                    </button>
                                    <button class="btn-warning btn-xs" onclick="app.teacherManager.resetLesson('${progress.userId}', '${lesson.lessonId}')">
                                        ↻ Сбросить
                                    </button>
                                </div>
                            </div>
                        `).join('')}
                    </div>
                </div>
            `).join('');
        }

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
                this.uiManager.showToast('Урок отмечен как завершенный', 'success');
                this.viewStudentProgress(studentId); 
            }
        } catch (error) {
            console.error('Ошибка:', error);
            this.uiManager.showToast('Ошибка при выполнении действия', 'error');
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
                this.uiManager.showToast('Прогресс урока сброшен', 'success');
                this.viewStudentProgress(studentId); 
            }
        } catch (error) {
            console.error('Ошибка:', error);
            this.uiManager.showToast('Ошибка при выполнении действия', 'error');
        }
    }

    editCourse(courseId) {
        this.uiManager.showToast('Редактирование курса в разработке', 'info');
    }

    manageLessons(courseId) {
        this.uiManager.showToast('Управление уроками в разработке', 'info');
    }
}