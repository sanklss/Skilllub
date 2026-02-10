export class CourseManager {
    constructor(apiService, uiManager, quizManager, authManager) {
        this.api = apiService;
        this.uiManager = uiManager;
        this.quizManager = quizManager;
        this.authManager = authManager;
        this.currentCourse = null;
        this.currentLesson = null;
        this.currentLessons = [];
        this.currentModule = null;
        this.allModules = [];
        this.isUserEnrolled = false;
        this.courseProgress = 0;
        this.isAuthenticated = false;
        this.userId = null;
    }

    async initialize() {
        this.setupCourseEventListeners();
        await this.loadCourses();
    }

    setupCourseEventListeners() {
        document.getElementById('prev-step')?.addEventListener('click', () => this.goToPreviousStep());
        document.getElementById('next-step')?.addEventListener('click', () => this.goToNextStep());
        document.getElementById('run-code')?.addEventListener('click', () => this.runCode());
        document.getElementById('reset-code')?.addEventListener('click', () => this.resetCode());
        document.getElementById('submit-code')?.addEventListener('click', () => this.submitCode());
        document.getElementById('search-input')?.addEventListener('input', this.debounce(this.searchCourses.bind(this), 300));
        document.getElementById('reset-filters')?.addEventListener('click', () => this.resetFilters());
    }

    async loadCourses() {
        try {
            const result = await this.api.getCourses();
            
            if (result.success) {
                this.renderCourses(result.courses);
            } else {
                this.uiManager.showToast('Ошибка загрузки курсов', 'error');
                this.renderCourses([]);
            }
        } catch (error) {
            console.error('Failed to load courses:', error);
            this.uiManager.showToast('Ошибка загрузки курсов', 'error');
            this.renderCourses([]);
        }
    }

    renderCourses(courses) {
        const coursesGrid = document.getElementById('courses-grid');
        if (!coursesGrid) return;

        if (courses.length === 0) {
            const template = document.getElementById('empty-courses-template');
            coursesGrid.innerHTML = template.innerHTML;
            return;
        }

        const courseCardTemplate = document.getElementById('course-card-template');
        let coursesHtml = '';

        courses.forEach(course => {
            let courseHtml = courseCardTemplate.innerHTML
                .replace(/{{id}}/g, course.id)
                .replace('{{title}}', this.escapeHtml(course.title))
                .replace('{{description}}', this.escapeHtml(course.description || 'Описание курса'))
                .replace('{{difficulty}}', course.difficultyLevel || 'beginner')
                .replace('{{difficultyText}}', this.getDifficultyText(course.difficultyLevel));
            
            coursesHtml += courseHtml;
        });

        coursesGrid.innerHTML = coursesHtml;
    }

    async openCourse(courseId) {
        try {
            this.uiManager.showSection('course-page');
            
            const courseResult = await this.api.getCourse(courseId);
            if (!courseResult.success) {
                throw new Error('Курс не найден');
            }

            this.currentCourse = courseResult.course;
            this.isAuthenticated = this.authManager.isAuthenticated();
            this.userId = this.isAuthenticated ? this.authManager.getCurrentUser()?.id : null;
            
            if (this.isAuthenticated) {
                const enrollmentResult = await this.api.checkEnrollment(courseId);
                if (enrollmentResult.success) {
                    this.isUserEnrolled = enrollmentResult.isEnrolled;
                    this.courseProgress = enrollmentResult.progress || 0;
                }
            } else {
                this.isUserEnrolled = false;
                this.courseProgress = 0;
            }

            const modulesResult = await this.api.getCourseModules(courseId, this.userId);
            
            if (modulesResult.success && modulesResult.modules.length > 0) {
                this.allModules = modulesResult.modules;
                
                let firstAccessibleModule = modulesResult.modules.find(m => m.isAccessible);
                
                if (this.isUserEnrolled && !firstAccessibleModule) {
                    firstAccessibleModule = modulesResult.modules[0];
                }
                
                if (firstAccessibleModule) {
                    await this.openModule(firstAccessibleModule.id);
                }
                
                this.renderCourseSidebar(this.currentCourse, modulesResult.modules);
            } else {
                this.allModules = [];
                this.renderCourseSidebar(this.currentCourse, []);
            }

            this.renderCourseAccessControls();

        } catch (error) {
            console.error('Failed to open course:', error);
            this.uiManager.showToast('Ошибка загрузки курса', 'error');
            this.uiManager.showSection('catalog');
        }
    }

    async openModule(moduleId) {
        if (!this.isUserEnrolled && this.isAuthenticated) {
            this.uiManager.showToast('Запишитесь на курс, чтобы открыть модуль', 'warning');
            return;
        }
        
        const module = this.allModules.find(m => m.id === moduleId);
        if (!module) {
            this.uiManager.showToast('Модуль не найден', 'error');
            return;
        }
        
        if (!module.isAccessible) {
            if (module.isCompleted) {
                this.uiManager.showToast('Этот модуль уже завершен', 'info');
            } else {
                this.uiManager.showToast('Этот модуль пока недоступен. Завершите предыдущий модуль.', 'warning');
            }
            return;
        }
        
        try {
            await this.loadModuleLessons(moduleId);
            
            if (this.currentLessons.length > 0) {
                await this.openLesson(this.currentLessons[0].id);
            }
            
            this.updateActiveModule(moduleId);
            
        } catch (error) {
            console.error('Failed to open module:', error);
            this.uiManager.showToast('Ошибка загрузки модуля', 'error');
        }
    }

    async loadModuleLessons(moduleId) {
        try {
            const result = await this.api.getModuleLessons(moduleId, this.userId);
            
            if (result.success) {
                this.currentLessons = result.lessons;
                this.currentModule = this.allModules.find(m => m.id === moduleId) || null;
                this.renderLessonsSidebar(result.lessons);
            } else {
                this.currentLessons = [];
                this.currentModule = null;
                this.uiManager.showToast('Уроки не найдены или модуль недоступен', 'warning');
            }
        } catch (error) {
            console.error('Failed to load module lessons:', error);
            this.uiManager.showToast('Ошибка загрузки уроков', 'error');
            this.currentLessons = [];
            this.currentModule = null;
        }
    }

    renderLessonsSidebar(lessons) {
        const moduleElement = document.querySelector(`[data-module-id="${this.currentModule?.id}"]`);
        if (!moduleElement) return;
        
        const lessonsList = moduleElement.querySelector('.lessons-list');
        if (!lessonsList) return;
        
        if (lessons.length === 0) {
            lessonsList.innerHTML = '<li class="muted">Уроки не найдены</li>';
            return;
        }
        
        let lessonsHtml = '';
        lessons.forEach(lesson => {
            lessonsHtml += `
                <li class="lesson-item" 
                    data-lesson-id="${lesson.id}"
                    onclick="app.courseManager.openLessonFromModule('${lesson.id}')">
                    <div class="lesson-icon">${lesson.order}</div>
                    <div class="lesson-info">
                        <div class="lesson-title">${lesson.title}</div>
                        <div class="lesson-status accessible">Доступен</div>
                    </div>
                </li>
            `;
        });
        
        lessonsList.innerHTML = lessonsHtml;
    }

    async openLessonFromModule(lessonId) {
        if (!this.isUserEnrolled) {
            this.uiManager.showToast('Запишитесь на курс, чтобы открыть урок', 'warning');
            return;
        }
        
        await this.openLesson(lessonId);
    }

    async openLesson(lessonId) {
        if (!this.isUserEnrolled) {
            this.uiManager.showToast('Запишитесь на курс, чтобы открыть урок', 'warning');
            return;
        }

        try {
            document.querySelectorAll('.lesson-item').forEach(item => {
                item.classList.remove('active');
            });
            
            const lessonElement = document.querySelector(`[data-lesson-id="${lessonId}"]`);
            if (lessonElement) {
                lessonElement.classList.add('active');
            }
            
            const result = await this.api.getLesson(lessonId, this.userId);
            
            if (result.success) {
                this.currentLesson = result.lesson;
                this.renderLessonContent(result.lesson);
                
                const hasCodeExercise = await this.checkIfLessonHasCodeExercise(lessonId);
                
                if (hasCodeExercise) {
                    const pythonLanguageId = '11111111-1111-1111-1111-111111111111';
                    await this.loadCodeTemplate(lessonId, pythonLanguageId);
                    this.uiManager.showCodeSection();
                } else {
                    this.uiManager.hideCodeSection();
                }
                
                await this.quizManager.loadQuizQuestions(lessonId);
                
            } else {
                this.uiManager.showToast('Урок не найден или недоступен', 'error');
            }
        } catch (error) {
            console.error('Failed to open lesson:', error);
            this.uiManager.showToast('Ошибка загрузки урока', 'error');
        }
    }

    renderLessonContent(lesson) {
        const stepTitle = document.getElementById('step-title');
        if (stepTitle) {
            stepTitle.textContent = lesson.title;
        }

        const stepNumber = document.getElementById('step-number');
        if (stepNumber) {
            stepNumber.textContent = `Урок ${lesson.order} из ${this.currentLessons.length}`;
        }

        const stepContent = document.querySelector('.step-content');
        if (stepContent) {
            stepContent.innerHTML = `
                <h2>${lesson.title}</h2>
                <p class="lesson-description">${lesson.description || ''}</p>
                <div class="lesson-content">
                    ${lesson.content ? lesson.content.replace(/\n/g, '<br>') : 'Контент урока пока не добавлен.'}
                </div>
            `;
        }
        
        this.updateNavigationButtons();
    }

    updateNavigationButtons() {
        const prevButton = document.getElementById('prev-step');
        const nextButton = document.getElementById('next-step');
        
        if (!this.currentLesson || this.currentLessons.length === 0) {
            if (prevButton) prevButton.disabled = true;
            if (nextButton) nextButton.disabled = true;
            return;
        }
        
        const currentIndex = this.currentLessons.findIndex(lesson => lesson.id === this.currentLesson.id);
        
        if (prevButton) {
            prevButton.disabled = currentIndex <= 0;
        }
        
        if (nextButton) {
            nextButton.disabled = currentIndex >= this.currentLessons.length - 1;
        }
    }

    goToPreviousStep() {
        if (!this.currentLesson || this.currentLessons.length === 0) return;
        
        const currentIndex = this.currentLessons.findIndex(lesson => lesson.id === this.currentLesson.id);
        if (currentIndex > 0) {
            const prevLesson = this.currentLessons[currentIndex - 1];
            this.openLesson(prevLesson.id);
        }
    }

    goToNextStep() {
        if (!this.currentLesson || this.currentLessons.length === 0) return;
        
        const currentIndex = this.currentLessons.findIndex(lesson => lesson.id === this.currentLesson.id);
        if (currentIndex < this.currentLessons.length - 1) {
            const nextLesson = this.currentLessons[currentIndex + 1];
            this.openLesson(nextLesson.id);
        }
    }

    renderCourseSidebar(course, modules) {
        const sidebarTitle = document.getElementById('sidebar-course-title');
        if (sidebarTitle) {
            sidebarTitle.textContent = course.title;
        }

        const breadcrumbCourse = document.getElementById('breadcrumb-course');
        if (breadcrumbCourse) {
            breadcrumbCourse.textContent = course.title;
        }

        const modulesList = document.querySelector('.modules-list');
        if (modulesList) {
            if (modules.length === 0) {
                modulesList.innerHTML = '<p class="muted">Модули не найдены</p>';
                return;
            }

            let modulesHtml = '';
            modules.forEach(module => {
                const statusIcon = module.isCompleted ? '✓' : 
                                 module.isAccessible ? '▶' : '🔒';
                const statusClass = module.isCompleted ? 'completed' : 
                                  module.isAccessible ? 'accessible' : 'locked';
                
                modulesHtml += `
                    <div class="module-item ${statusClass}" 
                         data-module-id="${module.id}">
                        <div class="module-header" onclick="app.courseManager.openModule('${module.id}')">
                            <span class="module-order">${module.order}.</span>
                            <span class="module-title">${module.title}</span>
                            <span class="module-status">${statusIcon}</span>
                        </div>
                        ${!module.isAccessible && !module.isCompleted ? 
                            '<div class="module-hint muted">Завершите предыдущий модуль</div>' : ''}
                        <ul class="lessons-list" id="lessons-${module.id}">
                            <li>${module.isAccessible ? 'Загрузка уроков...' : 'Модуль заблокирован'}</li>
                        </ul>
                    </div>
                `;
            });

            modulesList.innerHTML = modulesHtml;
        }
    }

    updateActiveModule(moduleId) {
        document.querySelectorAll('.module-item').forEach(item => {
            item.classList.remove('active');
        });
        
        const activeModule = document.querySelector(`[data-module-id="${moduleId}"]`);
        if (activeModule) {
            activeModule.classList.add('active');
        }
    }

    renderCourseAccessControls() {
        const accessControls = document.getElementById('course-access-controls');
        if (!accessControls) {
            const stepContainer = document.querySelector('.step-container');
            if (stepContainer) {
                const controlsDiv = document.createElement('div');
                controlsDiv.id = 'course-access-controls';
                controlsDiv.className = 'course-access-controls';
                stepContainer.parentNode.insertBefore(controlsDiv, stepContainer);
                this.renderCourseAccessControls();
            }
            return;
        }

        if (!this.isAuthenticated) {
            accessControls.innerHTML = `
                <div class="course-access-notice">
                    <div class="lock-icon-large">🔒</div>
                    <h3>Войдите, чтобы начать обучение</h3>
                    <p>Для прохождения курса необходимо войти в систему</p>
                    <div class="access-actions">
                        <button class="btn-primary" onclick="app.uiManager.showModal('modal-login')">
                            Войти
                        </button>
                        <button class="btn-secondary" onclick="app.uiManager.showModal('modal-signup')">
                            Зарегистрироваться
                        </button>
                    </div>
                </div>
            `;
        } else if (!this.isUserEnrolled) {
            accessControls.innerHTML = `
                <div class="course-access-notice">
                    <div class="lock-icon-large">🔓</div>
                    <h3>Готовы начать обучение?</h3>
                    <p>Запишитесь на курс, чтобы получить доступ ко всем модулям и урокам</p>
                    <div class="access-actions">
                        <button class="btn-primary" id="enroll-course-btn">
                            Приступить к обучению
                        </button>
                        <button class="btn-secondary" onclick="app.uiManager.showSection('catalog')">
                            Вернуться к каталогу
                        </button>
                    </div>
                </div>
            `;

            document.getElementById('enroll-course-btn')?.addEventListener('click', () => {
                this.enrollInCourse();
            });
        } else {
            accessControls.innerHTML = `
                <div class="course-progress-display">
                    <h3>Ваш прогресс: ${this.courseProgress}%</h3>
                    <div class="progress-bar-large">
                        <div class="progress-fill" style="width: ${this.courseProgress}%"></div>
                    </div>
                    <p class="muted">Модули открываются последовательно. Завершайте текущий модуль, чтобы открыть следующий.</p>
                </div>
            `;
        }
    }

    async enrollInCourse() {
        try {
            if (!this.currentCourse) return;
            
            this.uiManager.showButtonLoading('enroll-course-btn', true);
            
            const result = await this.api.enrollInCourse(this.currentCourse.id);
            
            if (result.success) {
                this.isUserEnrolled = true;
                this.courseProgress = 0;
                
                this.uiManager.showToast('Вы успешно записались на курс!', 'success');
                
                this.userId = this.authManager.getCurrentUser()?.id;
                const modulesResult = await this.api.getCourseModules(this.currentCourse.id, this.userId);
                
                if (modulesResult.success) {
                    this.allModules = modulesResult.modules;
                    this.renderCourseSidebar(this.currentCourse, this.allModules);
                    this.renderCourseAccessControls();
                    
                    const firstAccessibleModule = this.allModules.find(m => m.isAccessible);
                    if (firstAccessibleModule) {
                        await this.openModule(firstAccessibleModule.id);
                    }
                }
            } else {
                throw new Error(result.error || 'Не удалось записаться на курс');
            }
        } catch (error) {
            console.error('Ошибка записи на курс:', error);
            this.uiManager.showToast(error.message, 'error');
        } finally {
            this.uiManager.showButtonLoading('enroll-course-btn', false);
        }
    }

    async checkIfLessonHasCodeExercise(lessonId) {
        try {
            const pythonLanguageId = '11111111-1111-1111-1111-111111111111';
            const result = await this.api.getCodeTemplate(lessonId, pythonLanguageId, this.userId);
            
            return result.success && result.template && 
                   (result.template.starterCode || result.template.templateCode);
        } catch (error) {
            console.log('No code exercise found for lesson:', lessonId);
            return false;
        }
    }

    async loadCodeTemplate(lessonId, languageId) {
        try {
            const result = await this.api.getCodeTemplate(lessonId, languageId, this.userId);
            
            if (result.success && result.template) {
                const codeEditor = document.getElementById('code-editor');
                if (codeEditor) {
                    codeEditor.value = result.template.starterCode || result.template.templateCode || '';
                    codeEditor.disabled = false;
                }
                
                const runBtn = document.getElementById('run-code');
                const resetBtn = document.getElementById('reset-code');
                const submitBtn = document.getElementById('submit-code');
                
                if (runBtn) runBtn.disabled = false;
                if (resetBtn) resetBtn.disabled = false;
                if (submitBtn) submitBtn.disabled = false;
            }
        } catch (error) {
            console.error('Failed to load code template:', error);
        }
    }

    runCode() {
        if (!this.isUserEnrolled) {
            this.uiManager.showToast('Запишитесь на курс, чтобы выполнять задания', 'warning');
            return;
        }
        const code = document.getElementById('code-editor').value;
        const language = document.getElementById('language-select').value;
        
        console.log('Running code:', { code, language });
        document.getElementById('results-section').classList.remove('hidden');
    }

    resetCode() {
        if (!this.isUserEnrolled) {
            this.uiManager.showToast('Запишитесь на курс, чтобы выполнять задания', 'warning');
            return;
        }
        document.getElementById('code-editor').value = '';
        document.getElementById('results-section').classList.add('hidden');
    }

    async submitCode() {
        if (!this.isUserEnrolled) {
            this.uiManager.showToast('Запишитесь на курс, чтобы выполнять задания', 'warning');
            return;
        }
        
        const code = document.getElementById('code-editor').value;
        const language = document.getElementById('language-select').value;
        
        console.log('Submitting code:', { code, language });
        
        if (this.currentLesson) {
            try {
                const result = await this.api.completeLesson(this.currentLesson.id);
                if (result.success) {
                    this.uiManager.showToast('Решение отправлено! Урок завершен.', 'success');
                }
            } catch (error) {
                console.error('Failed to complete lesson:', error);
                this.uiManager.showToast('Ошибка при завершении урока', 'error');
            }
        } else {
            this.uiManager.showToast('Решение отправлено на проверку!', 'success');
        }
    }

    searchCourses(event) {
        const searchTerm = event.target.value.toLowerCase();
        const courseCards = document.querySelectorAll('.course-card');
        
        courseCards.forEach(card => {
            const title = card.querySelector('.course-title').textContent.toLowerCase();
            const description = card.querySelector('.course-description').textContent.toLowerCase();
            
            if (title.includes(searchTerm) || description.includes(searchTerm)) {
                card.style.display = 'block';
            } else {
                card.style.display = 'none';
            }
        });
    }

    resetFilters() {
        document.getElementById('category-filter').value = '';
        document.getElementById('difficulty-filter').value = '';
        
        document.querySelectorAll('.course-card').forEach(card => {
            card.style.display = 'block';
        });
    }

    getDifficultyText(difficulty) {
        const difficulties = {
            'beginner': 'Начальный',
            'intermediate': 'Средний',
            'advanced': 'Продвинутый'
        };
        return difficulties[difficulty] || 'Начальный';
    }

    escapeHtml(unsafe) {
        return unsafe
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
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