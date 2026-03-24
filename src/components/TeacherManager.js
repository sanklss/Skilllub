import { ApiService } from '../services/ApiService.js';
import { TeacherCourseCreator } from './TeacherCourseCreator.js';

export class TeacherManager {
    constructor(apiService, uiManager) {
        this.courseCreator = new TeacherCourseCreator(apiService, uiManager);
        this.api = apiService;
        this.uiManager = uiManager;
        this.currentView = 'dashboard';
        this.currentCourseId = null;
        this.currentStudentId = null;
        this.courses = [];
        this.students = [];
        this.allStudentsData = []; 
        this.teacherCourses = []; 
    }

    async initialize() {
        this.setupTeacherEventListeners();
        await this.loadDashboard();
        
        if (this.courseCreator) {
            this.courseCreator.initialize();
        }
    }

    setupTeacherEventListeners() {
        document.getElementById('teacher-nav-dashboard')?.addEventListener('click', () => this.showTeacherView('dashboard'));
        document.getElementById('teacher-nav-courses')?.addEventListener('click', () => this.showTeacherView('courses'));
        document.getElementById('teacher-nav-students')?.addEventListener('click', () => this.showTeacherView('students'));
        document.getElementById('teacher-nav-statistics')?.addEventListener('click', () => this.showTeacherView('statistics'));
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
                    Студенты
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
            container.innerHTML = `
                <div class="empty-state" style="text-align: center; padding: 40px;">
                    <div style="font-size: 48px; margin-bottom: 20px;"></div>
                    <h3>У вас пока нет курсов</h3>
                    <p class="muted">Создайте свой первый курс!</p>
                    <button class="btn-primary" id="create-course-btn" style="margin-top: 20px;">
                        + Создать курс
                    </button>
                </div>
            `;
            
            document.getElementById('create-course-btn')?.addEventListener('click', () => {
                if (window.app && window.app.teacherManager && window.app.teacherManager.courseCreator) {
                    window.app.teacherManager.courseCreator.showCourseCreationModal();
                }
            });
            return;
        }

        container.innerHTML = courses.map(course => `
            <div class="course-card teacher-course-card">
                <h3>${course.title}</h3>
                <p class="description">${course.description || 'Нет описания'}</p>
                <div class="course-meta">
                    <span> Прогресс: ${Math.round(course.averageProgress)}%</span>
                    <span> ${course.studentCount} студентов</span>
                </div>
                <div class="course-actions">
                    <button class="btn-secondary btn-sm" onclick="app.teacherManager.editCourse('${course.id}')">
                         Редактировать
                    </button>
                    <button class="btn-secondary btn-sm" onclick="app.teacherManager.manageLessons('${course.id}')">
                         Уроки
                    </button>
                </div>
            </div>
        `).join('');
    }

    async loadTeacherStudents() {
        try {
            const dashboard = await this.api.getTeacherDashboard();
            
            if (!dashboard.success || !dashboard.dashboard.courses) {
                return;
            }

            this.teacherCourses = dashboard.dashboard.courses;
            
            const filter = document.getElementById('student-course-filter');
            if (filter) {
                filter.innerHTML = '<option value="">Все курсы</option>' + 
                    this.teacherCourses.map(c => 
                        `<option value="${c.id}">${c.title} (${c.studentCount} студ.)</option>`
                    ).join('');
                
                filter.removeEventListener('change', this.handleCourseFilter);
                this.handleCourseFilter = (e) => {
                    const courseId = e.target.value;
                    if (courseId) {
                        this.loadCourseStudents(courseId);
                    } else {
                        this.loadAllStudents();
                    }
                };
                filter.addEventListener('change', this.handleCourseFilter);
            }
            
            await this.loadAllStudents();
            
        } catch (error) {
            console.error('Ошибка загрузки студентов:', error);
            this.uiManager.showToast('Ошибка загрузки студентов', 'error');
        }
    }

    async loadAllStudents() {
        try {
            const container = document.getElementById('teacher-students-list');
            if (!container || !this.teacherCourses.length) {
                container.innerHTML = '<p class="muted">У вас пока нет студентов</p>';
                return;
            }
            
            let allStudents = [];
            const studentsMap = new Map(); 
            
            for (const course of this.teacherCourses) {
                const result = await this.api.getCourseStudents(course.id);
                if (result.success && result.students) {
                    result.students.forEach(student => {
                        if (!studentsMap.has(student.userId)) {
                            studentsMap.set(student.userId, {
                                userId: student.userId,
                                username: student.username,
                                email: student.email,
                                courses: []
                            });
                        }
                        
                        studentsMap.get(student.userId).courses.push({
                            courseId: course.id,
                            courseTitle: course.title,
                            progress: student.courseProgress,
                            enrolledAt: student.enrolledAt
                        });
                    });
                }
            }
            
            this.allStudentsData = Array.from(studentsMap.values());
            this.renderAllStudents(this.allStudentsData);
            
        } catch (error) {
            console.error('Ошибка загрузки всех студентов:', error);
            this.uiManager.showToast('Ошибка загрузки студентов', 'error');
        }
    }

    renderAllStudents(students) {
        const container = document.getElementById('teacher-students-list');
        if (!container) return;
        
        if (!students || students.length === 0) {
            container.innerHTML = '<p class="muted">У вас пока нет студентов</p>';
            return;
        }
        
        let html = `
            <table class="students-table">
                <thead>
                    <tr>
                        <th>Студент</th>
                        <th>Email</th>
                        <th>Курсы</th>
                        <th>Средний прогресс</th>
                        <th>Действия</th>
                    </tr>
                </thead>
                <tbody>
        `;
        
        students.forEach(student => {
            const avgProgress = Math.round(
                student.courses.reduce((sum, c) => sum + c.progress, 0) / student.courses.length
            );
            
            const coursesList = student.courses.map(c => 
                `<div class="course-badge">
                    ${c.courseTitle}: <strong>${c.progress}%</strong>
                    <button class="btn-icon btn-xs" onclick="app.teacherManager.viewStudentCourseProgress('${student.userId}', '${c.courseId}')">
                        Смотреть
                    </button>
                </div>`
            ).join('');
            
            html += `
                <tr>
                    <td><strong>${student.username}</strong></td>
                    <td>${student.email}</td>
                    <td><div class="courses-list">${coursesList}</div></td>
                    <td>
                        <div class="progress-bar-small">
                            <div class="progress-fill" style="width: ${avgProgress}%"></div>
                        </div>
                        ${avgProgress}%
                    </td>
                    <td>
                        <button class="btn-secondary btn-sm" 
                                onclick="app.teacherManager.showStudentSelector('${student.userId}')">
                             Выбрать курс
                        </button>
                    </td>
                </tr>
            `;
        });
        
        html += '</tbody></table>';
        container.innerHTML = html;
    }

    showStudentSelector(studentId) {
        const student = this.allStudentsData?.find(s => s.userId === studentId);
        if (!student || !student.courses.length) return;
        
        const oldModal = document.getElementById('student-course-selector');
        if (oldModal) oldModal.remove();
        
        const modal = document.createElement('div');
        modal.id = 'student-course-selector';
        modal.className = 'modal hidden';
        
        modal.innerHTML = `
            <div class="modal-card" style="max-width: 400px;">
                <div class="modal-header" style="display: flex; justify-content: space-between; align-items: center; padding: 15px 20px; border-bottom: 1px solid #e2e8f0;">
                    <h3 style="margin: 0;">Выберите курс</h3>
                    <button class="close-btn" onclick="this.closest('.modal').classList.add('hidden')">✕</button>
                </div>
                <div class="modal-content" style="padding: 20px;">
                    <p class="muted" style="margin-bottom: 15px;">Студент: <strong>${student.username}</strong></p>
                    <div style="display: flex; flex-direction: column; gap: 10px;">
                        ${student.courses.map(c => `
                            <button class="course-select-btn" 
                                    style="width: 100%; padding: 12px; border: 1px solid #e2e8f0; border-radius: 8px; background: white; cursor: pointer; text-align: left; transition: all 0.2s;"
                                    onmouseover="this.style.backgroundColor='#f7fafc'; this.style.borderColor='#4299e1';"
                                    onmouseout="this.style.backgroundColor='white'; this.style.borderColor='#e2e8f0';"
                                    onclick="app.teacherManager.viewStudentCourseProgress('${studentId}', '${c.courseId}'); this.closest('.modal').classList.add('hidden');">
                                <div style="font-weight: 600;">${c.courseTitle}</div>
                                <div style="display: flex; align-items: center; gap: 10px; margin-top: 5px;">
                                    <div style="flex: 1; height: 6px; background: #e2e8f0; border-radius: 3px;">
                                        <div style="height: 100%; width: ${c.progress}%; background: #4299e1; border-radius: 3px;"></div>
                                    </div>
                                    <span style="font-size: 14px; color: #4a5568;">${c.progress}%</span>
                                </div>
                            </button>
                        `).join('')}
                    </div>
                </div>
                <div class="modal-actions" style="padding: 15px 20px; border-top: 1px solid #e2e8f0; text-align: right;">
                    <button class="btn-secondary" onclick="this.closest('.modal').classList.add('hidden')">
                        Закрыть
                    </button>
                </div>
            </div>
        `;
        
        document.body.appendChild(modal);
        setTimeout(() => modal.classList.remove('hidden'), 10);
    }

    async viewStudentCourseProgress(studentId, courseId) {
        if (!courseId) {
            this.uiManager.showToast('Ошибка: не указан курс', 'error');
            return;
        }

        try {
            this.uiManager.showLoading(true);
            const result = await this.api.getStudentProgress(studentId, courseId);
            
            if (result.success) {
                const course = this.teacherCourses.find(c => c.id === courseId);
                if (course) {
                    result.progress.courseTitle = course.title;
                }
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

    async loadCourseStudents(courseId) {
        try {
            const container = document.getElementById('teacher-students-list');
            if (!container) return;
            
            const result = await this.api.getCourseStudents(courseId);
            if (result.success) {
                this.renderStudentsList(result.students, courseId);
            }
        } catch (error) {
            console.error('Ошибка загрузки студентов курса:', error);
            this.uiManager.showToast('Ошибка загрузки студентов', 'error');
        }
    }

    renderStudentsList(students, courseId) {
        const container = document.getElementById('teacher-students-list');
        if (!container) return;

        if (!students || students.length === 0) {
            container.innerHTML = '<p class="muted">На этом курсе пока нет студентов</p>';
            return;
        }

        const course = this.teacherCourses.find(c => c.id === courseId);
        const courseTitle = course ? course.title : 'текущий курс';

        container.innerHTML = `
            <div style="margin-bottom: 15px;">
                <h3 style="margin: 0;">Студенты курса: ${courseTitle}</h3>
                <p class="muted" style="margin-top: 5px;">Всего: ${students.length}</p>
            </div>
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
                            <td><strong>${student.username}</strong></td>
                            <td>${student.email}</td>
                            <td>
                                <div style="display: flex; align-items: center; gap: 8px;">
                                    <div class="progress-bar-small" style="width: 100px;">
                                        <div class="progress-fill" style="width: ${student.courseProgress}%"></div>
                                    </div>
                                    <span>${student.courseProgress}%</span>
                                </div>
                            </td>
                            <td>${new Date(student.enrolledAt).toLocaleDateString()}</td>
                            <td>
                                <button class="btn-secondary btn-xs" 
                                        onclick="app.teacherManager.viewStudentCourseProgress('${student.userId}', '${courseId}')">
                                     Детали
                                </button>
                            </td>
                        </tr>
                    `).join('')}
                </tbody>
            </table>
            <div style="margin-top: 15px;">
                <button class="btn-secondary btn-sm" onclick="app.teacherManager.loadAllStudents()">
                    ← К списку всех студентов
                </button>
            </div>
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
                    <div class="stat-item" style="margin-bottom: 10px;">
                        <div style="font-weight: 600;">${c.title}</div>
                        <div style="display: flex; justify-content: space-between; margin-top: 5px;">
                            <span> ${c.studentCount} студентов</span>
                            <span> ${Math.round(c.averageProgress)}%</span>
                        </div>
                    </div>
                `).join('');
            }
        }
    }

    async viewCourseStudents(courseId) {
        await this.showTeacherView('students');
        await this.loadCourseStudents(courseId);
    }

    showStudentProgressModal(progress) {
        let modal = document.getElementById('student-progress-modal');
        
        if (!modal) {
            modal = document.createElement('div');
            modal.id = 'student-progress-modal';
            modal.className = 'modal hidden';
            
            modal.innerHTML = `
                <div class="modal-card" style="max-width: 900px; width: 95%; max-height: 85vh; overflow-y: auto;">
                    <div class="modal-header" style="display: flex; justify-content: space-between; align-items: center; padding: 20px; border-bottom: 1px solid #e2e8f0; position: sticky; top: 0; background: white; z-index: 10;">
                        <h3 style="margin: 0;">Прогресс студента: <span id="modal-student-name"></span></h3>
                        <button class="btn-close" onclick="document.getElementById('student-progress-modal').classList.add('hidden')" style="background: none; border: none; font-size: 24px; cursor: pointer;">✕</button>
                    </div>
                    
                    <div class="modal-content" style="padding: 20px;">
                        <div id="modal-course-info" style="margin-bottom: 15px; padding: 10px; background: #f0f9ff; border-radius: 8px; border-left: 4px solid #4299e1;"></div>
                        
                        <div class="student-summary" style="background: linear-gradient(135deg, #f8f9fa, #e9ecef); padding: 20px; border-radius: 12px; margin-bottom: 24px;">
                            <div style="display: flex; gap: 30px; flex-wrap: wrap;">
                                <div><strong>Email:</strong> <span id="modal-student-email"></span></div>
                                <div><strong>Всего уроков:</strong> <span id="modal-total-lessons">0</span></div>
                                <div><strong>Пройдено:</strong> <span id="modal-completed-lessons">0</span></div>
                                <div><strong>Прогресс:</strong> <span id="modal-progress-percent">0%</span></div>
                            </div>
                            <div class="progress-bar-large" style="height: 8px; background: #e0e0e0; border-radius: 4px; margin-top: 15px;">
                                <div class="progress-fill" id="modal-progress-bar" style="height: 100%; background: linear-gradient(90deg, #4299e1, #1d4ed8); border-radius: 4px; width: 0%; transition: width 0.3s;"></div>
                            </div>
                        </div>
                        
                        <div id="modal-modules-list" class="modules-progress" style="display: flex; flex-direction: column; gap: 20px;"></div>
                    </div>
                </div>
            `;
            
            document.body.appendChild(modal);
        }

        document.getElementById('modal-student-name').textContent = progress.username || 'Неизвестно';
        document.getElementById('modal-student-email').textContent = progress.email || 'Не указан';
        document.getElementById('modal-total-lessons').textContent = progress.totalLessons || 0;
        document.getElementById('modal-completed-lessons').textContent = progress.completedLessons || 0;
        
        const courseInfoEl = document.getElementById('modal-course-info');
        if (courseInfoEl && progress.courseTitle) {
            courseInfoEl.innerHTML = ` <strong>Курс:</strong> ${progress.courseTitle}`;
        }
        
        const percent = progress.totalLessons > 0 
            ? Math.round((progress.completedLessons / progress.totalLessons) * 100) 
            : 0;
        document.getElementById('modal-progress-percent').textContent = percent + '%';
        document.getElementById('modal-progress-bar').style.width = percent + '%';

        const modulesContainer = document.getElementById('modal-modules-list');
        
        if (!progress.modules || progress.modules.length === 0) {
            modulesContainer.innerHTML = '<p class="muted" style="text-align: center; padding: 40px;">Нет данных о прогрессе</p>';
        } else {
            modulesContainer.innerHTML = progress.modules.map(module => `
                <div class="module-progress-card" style="border: 1px solid #e2e8f0; border-radius: 12px; overflow: hidden; background: white;">
                    <div class="module-header" style="padding: 16px 20px; background: linear-gradient(135deg, #f8f9fa, #e9ecef); cursor: pointer; display: flex; justify-content: space-between; align-items: center;" 
                         onclick="this.nextElementSibling.classList.toggle('hidden')">
                        <div>
                            <h4 style="margin: 0; font-size: 1.1rem;">${module.moduleTitle}</h4>
                            <div style="font-size: 0.9rem; color: #718096; margin-top: 4px;">
                                Прогресс: ${module.completedLessons || 0}/${module.totalLessons || 0} уроков
                            </div>
                        </div>
                        <div style="display: flex; align-items: center; gap: 15px;">
                            <span style="font-size: 0.9rem; font-weight: 600; color: ${module.isCompleted ? '#28a745' : '#6c757d'};">
                                ${module.isCompleted ? '✅ Завершен' : '⏳ В процессе'}
                            </span>
                            <span style="font-size: 20px;">▼</span>
                        </div>
                    </div>
                    
                    <div class="module-lessons" style="padding: 16px;">
                        ${module.lessons.map(lesson => 
                            this.renderLessonProgress(lesson, progress.userId)
                        ).join('')}
                    </div>
                </div>
            `).join('');
        }

        modal.classList.remove('hidden');
    }

    renderLessonProgress(lesson, studentId) {
        let statusClass = '';
        let statusText = '';
        let statusColor = '';
        
        if (lesson.isCompleted) {
            statusClass = 'completed';
            statusText = '✅ Завершен';
            statusColor = '#28a745';
        } else if (lesson.theoryCompleted || lesson.quizCompleted || lesson.codeCompleted) {
            statusClass = 'in-progress';
            statusText = '⏳ В процессе';
            statusColor = '#ffc107';
        } else {
            statusClass = 'not-started';
            statusText = '📝 Не начат';
            statusColor = '#6c757d';
        }

        return `
            <div class="lesson-progress-item" style="display: flex; align-items: center; justify-content: space-between; padding: 12px 16px; margin: 8px 0; background: #f8f9fa; border-radius: 10px; border-left: 4px solid ${statusColor};">
                <div style="display: flex; align-items: center; gap: 12px; flex: 1;">
                    <span style="font-weight: 600; color: #4a5568; min-width: 30px;">${lesson.lessonOrder}.</span>
                    <span style="font-weight: 500;">${lesson.lessonTitle}</span>
                </div>
                
                <div style="display: flex; align-items: center; gap: 20px;">
                    <span style="font-size: 0.9rem; color: ${statusColor}; font-weight: 500; min-width: 100px;">
                        ${statusText}
                    </span>
                    
                    <div style="display: flex; gap: 8px;">
                        <button class="btn-primary btn-sm" onclick="app.teacherManager.markLessonCompleted('${studentId}', '${lesson.lessonId}')">
                            Завершить
                        </button>
                        <button class="btn-secondary btn-sm" onclick="app.teacherManager.resetLesson('${studentId}', '${lesson.lessonId}')">
                            Сбросить
                        </button>
                    </div>
                </div>
            </div>
        `;
    }

    async markLessonCompleted(studentId, lessonId) {
        if (!studentId || !lessonId) {
            console.error('❌ Ошибка: отсутствуют ID', { studentId, lessonId });
            this.uiManager.showToast('Ошибка: не указан ID урока или студента', 'error');
            return;
        }

        if (!confirm('Отметить урок как завершенный для этого студента?')) return;
        
        try {
            console.log('🔍 Отправка запроса на завершение урока:', { studentId, lessonId });
            
            const result = await this.api.performTeacherAction({
                userId: studentId,
                lessonId: lessonId,
                action: 'complete'
            });
            
            if (result.success) {
                this.uiManager.showToast('Урок отмечен как завершенный', 'success');
                const modal = document.getElementById('student-progress-modal');
                if (modal && !modal.classList.contains('hidden')) {
                    const courseInfo = document.getElementById('modal-course-info')?.textContent;
                    const courseMatch = courseInfo?.match(/Курс:\s*(.+)$/);
                    if (courseMatch) {
                        const courseTitle = courseMatch[1];
                        const course = this.teacherCourses.find(c => c.title === courseTitle);
                        if (course) {
                            await this.viewStudentCourseProgress(studentId, course.id);
                        }
                    }
                }
            } else {
                this.uiManager.showToast('Ошибка при выполнении действия', 'error');
            }
        } catch (error) {
            console.error('Ошибка:', error);
            this.uiManager.showToast('Ошибка при выполнении действия', 'error');
        }
    }

    async resetLesson(studentId, lessonId) {
        if (!studentId || !lessonId) {
            console.error('❌ Ошибка: отсутствуют ID', { studentId, lessonId });
            this.uiManager.showToast('Ошибка: не указан ID урока или студента', 'error');
            return;
        }

        if (!confirm('Сбросить прогресс урока для этого студента? Это действие нельзя отменить.')) return;
        
        try {
            console.log('🔍 Отправка запроса на сброс урока:', { studentId, lessonId });
            
            const result = await this.api.performTeacherAction({
                userId: studentId,
                lessonId: lessonId,
                action: 'reset'
            });
            
            if (result.success) {
                this.uiManager.showToast('Прогресс урока сброшен', 'success');
                const modal = document.getElementById('student-progress-modal');
                if (modal && !modal.classList.contains('hidden')) {
                    const courseInfo = document.getElementById('modal-course-info')?.textContent;
                    const courseMatch = courseInfo?.match(/Курс:\s*(.+)$/);
                    if (courseMatch) {
                        const courseTitle = courseMatch[1];
                        const course = this.teacherCourses.find(c => c.title === courseTitle);
                        if (course) {
                            await this.viewStudentCourseProgress(studentId, course.id);
                        }
                    }
                }
            } else {
                this.uiManager.showToast('Ошибка при выполнении действия', 'error');
            }
        } catch (error) {
            console.error('Ошибка:', error);
            this.uiManager.showToast('Ошибка при выполнении действия', 'error');
        }
    }

    editCourse(courseId) {
        console.log('📝 Редактирование курса:', courseId);
        if (this.courseCreator) {
            this.courseCreator.showCourseEditor(courseId);
        } else {
            this.uiManager.showToast('Ошибка: редактор не инициализирован', 'error');
        }
    }

    manageLessons(courseId) {
        console.log('📝 Управление уроками курса:', courseId);
        if (this.courseCreator) {
            this.courseCreator.showCourseEditor(courseId);
        } else {
            this.uiManager.showToast('Ошибка: редактор не инициализирован', 'error');
        }
    }
}