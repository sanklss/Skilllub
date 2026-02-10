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

            const modulesResult = await this.api.getCourseModules(courseId);
            
            if (modulesResult.success && modulesResult.modules.length > 0) {
                this.allModules = modulesResult.modules;
                const firstModule = modulesResult.modules[0];
                await this.loadModuleLessons(firstModule.id);
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

    async loadModuleLessons(moduleId) {
        try {
            const result = await this.api.getModuleLessons(moduleId);
            
            if (result.success) {
                this.currentLessons = result.lessons;
                await this.loadCurrentModule(moduleId);
                this.renderLessonsSidebar(result.lessons);
                
                if (result.lessons.length > 0 && this.isUserEnrolled) {
                    await this.openLesson(result.lessons[0].id);
                }
            }
        } catch (error) {
            console.error('Failed to load module lessons:', error);
        }
    }

    async loadCurrentModule(moduleId) {
        try {
            this.currentModule = this.allModules.find(module => module.id === moduleId) || null;
        } catch (error) {
            console.error('Failed to load current module:', error);
            this.currentModule = null;
        }
    }

    renderCourseSidebar(course, modules) {
        const sidebarTitle = document.getElementById('sidebar-course-title');
        if (sidebarTitle) {
            sidebarTitle.textContent = course.title;
        }

        const breadcrumb = document.querySelector('.breadcrumb');
        if (breadcrumb) {
            breadcrumb.innerHTML = `
                <a href="#catalog" onclick="app.uiManager.showSection('catalog')">Каталог курсов</a> 
                <span class="breadcrumb-separator">/</span>
                <span>${course.title}</span>
            `;
        }

        const modulesList = document.querySelector('.modules-list');
        if (modulesList) {
            if (modules.length === 0) {
                modulesList.innerHTML = '<p class="muted">Модули не найдены</p>';
                return;
            }

            let modulesHtml = '';
            modules.forEach(module => {
                modulesHtml += `
                    <div class="module-item">
                        <div class="module-header">
                            <span>${module.title}</span>
                            ${!this.isUserEnrolled ? '<span class="module-lock">🔒</span>' : ''}
                        </div>
                        <ul class="lessons-list" id="lessons-${module.id}">
                            <li>Загрузка уроков...</li>
                        </ul>
                    </div>
                `;
            });

            modulesList.innerHTML = modulesHtml;

            modules.forEach(module => {
                this.loadModuleLessonsForSidebar(module.id);
            });
        }
    }

    async loadModuleLessonsForSidebar(moduleId) {
        try {
            const result = await this.api.getModuleLessons(moduleId);
            const lessonsList = document.getElementById(`lessons-${moduleId}`);
            
            if (lessonsList && result.success) {
                let lessonsHtml = '';
                result.lessons.forEach(lesson => {
                    const isLocked = !this.isUserEnrolled;
                    const lockIcon = isLocked ? '<span class="lock-icon-small">🔒</span>' : '';
                    
                    if (isLocked) {
                        lessonsHtml += `
                            <li class="lesson-item locked" data-lesson-id="${lesson.id}">
                                <div class="lesson-icon">🔒</div>
                                <div class="lesson-info">
                                    <div class="lesson-title">${lesson.title} ${lockIcon}</div>
                                    <div class="lesson-status muted">Запишитесь на курс</div>
                                </div>
                            </li>
                        `;
                    } else {
                        lessonsHtml += `
                            <li class="lesson-item" data-lesson-id="${lesson.id}" 
                                onclick="app.courseManager.openLessonFromModule('${lesson.id}', '${moduleId}')">
                                <div class="lesson-icon">${lesson.order}</div>
                                <div class="lesson-info">
                                    <div class="lesson-title">${lesson.title}</div>
                                    <div class="lesson-status success">Доступен</div>
                                </div>
                            </li>
                        `;
                    }
                });
                
                lessonsList.innerHTML = lessonsHtml;
            }
        } catch (error) {
            console.error('Failed to load lessons for sidebar:', error);
        }
    }

    renderLessonsSidebar(lessons) {
        // Заглушка, если нужно
    }

    async openLesson(lessonId) {
        if (!this.isUserEnrolled && this.isAuthenticated) {
            this.uiManager.showToast('Запишитесь на курс, чтобы открыть урок', 'warning');
            return;
        }
        
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
            
            const result = await this.api.getLesson(lessonId);
            
            if (result.success) {
                this.currentLesson = result.lesson;
                this.renderLessonContent(result.lesson);
                
                const hasCodeExercise = await this.checkIfLessonHasCodeExercise(lessonId);
                
                if (hasCodeExercise && this.isUserEnrolled) {
                    const pythonLanguageId = '11111111-1111-1111-1111-111111111111';
                    await this.loadCodeTemplate(lessonId, pythonLanguageId);
                    this.uiManager.showCodeSection();
                } else {
                    this.uiManager.hideCodeSection();
                }
                
                if (this.isUserEnrolled) {
                    await this.quizManager.loadQuizQuestions(lessonId);
                } else {
                    this.uiManager.hideQuizSection();
                }
            }
        } catch (error) {
            console.error('Failed to open lesson:', error);
        }
    }

    async openLessonFromModule(lessonId, moduleId) {
        await this.loadCurrentModule(moduleId);
        await this.openLesson(lessonId);
    }

    renderCourseAccessControls() {
        const accessControls = document.getElementById('course-access-controls');
        if (!accessControls) {
            // Создаем контейнер если его нет
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
                    <p>Запишитесь на курс, чтобы получить доступ ко всем урокам и заданиям</p>
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
                    <p class="muted">Продолжайте обучение! Открывайте уроки из списка слева.</p>
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
                this.renderCourseAccessControls();
                
                await this.loadModuleLessonsForAllModules();
                
                if (this.allModules.length > 0 && this.allModules[0]) {
                    await this.loadModuleLessons(this.allModules[0].id);
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

    async loadModuleLessonsForAllModules() {
        for (const module of this.allModules) {
            await this.loadModuleLessonsForSidebar(module.id);
        }
    }

    renderLessonContent(lesson) {
        const stepTitle = document.getElementById('step-title');
        if (stepTitle) {
            stepTitle.textContent = this.currentModule?.title || this.currentCourse?.title || 'Курс';
        }

        const stepNumber = document.getElementById('step-number');
        if (stepNumber) {
            stepNumber.textContent = `Шаг ${lesson.order} из ${this.currentLessons.length}`;
        }

        const stepContent = document.querySelector('.step-content');
        if (stepContent) {
            if (!this.isUserEnrolled) {
                stepContent.innerHTML = `
                    <div class="lesson-preview">
                        <h2>${lesson.title}</h2>
                        <p class="lesson-description">${lesson.description || ''}</p>
                        <div class="lesson-preview-content">
                            ${lesson.content ? lesson.content.substring(0, 500) + '...' : 'Просмотр контента доступен после записи на курс.'}
                        </div>
                        <div class="preview-overlay">
                            <div class="overlay-content">
                                <div class="lock-icon-medium">🔒</div>
                                <h4>Запишитесь на курс для полного доступа</h4>
                                <p>Чтобы просмотреть полное содержание урока и выполнить задания, запишитесь на курс.</p>
                            </div>
                        </div>
                    </div>
                `;
            } else {
                stepContent.innerHTML = `
                    <h2>${lesson.title}</h2>
                    <p class="lesson-description">${lesson.description || ''}</p>
                    <div class="lesson-content">
                        ${lesson.content ? lesson.content.replace(/\n/g, '<br>') : 'Контент урока пока не добавлен.'}
                    </div>
                `;
            }
        }
    }

    goToPreviousStep() {
        if (!this.currentLesson || this.currentLessons.length === 0) return;
        
        const currentIndex = this.currentLessons.findIndex(lesson => lesson.id === this.currentLesson.id);
        if (currentIndex > 0) {
            const prevLesson = this.currentLessons[currentIndex - 1];
            
            const prevLessonModuleId = this.findModuleIdByLessonId(prevLesson.id);
            if (prevLessonModuleId) {
                this.openLessonFromModule(prevLesson.id, prevLessonModuleId);
            } else {
                this.openLesson(prevLesson.id);
            }
        }
    }

    goToNextStep() {
        if (!this.currentLesson || this.currentLessons.length === 0) return;
        
        const currentIndex = this.currentLessons.findIndex(lesson => lesson.id === this.currentLesson.id);
        if (currentIndex < this.currentLessons.length - 1) {
            const nextLesson = this.currentLessons[currentIndex + 1];
            
            const nextLessonModuleId = this.findModuleIdByLessonId(nextLesson.id);
            if (nextLessonModuleId) {
                this.openLessonFromModule(nextLesson.id, nextLessonModuleId);
            } else {
                this.openLesson(nextLesson.id);
            }
        }
    }

    findModuleIdByLessonId(lessonId) {
        for (const module of this.allModules) {
            const moduleLessons = document.querySelectorAll(`#lessons-${module.id} .lesson-item`);
            for (const lessonElement of moduleLessons) {
                if (lessonElement.getAttribute('data-lesson-id') === lessonId) {
                    return module.id;
                }
            }
        }
        return null;
    }

    async checkIfLessonHasCodeExercise(lessonId) {
        try {
            const pythonLanguageId = '11111111-1111-1111-1111-111111111111';
            const result = await this.api.getCodeTemplate(lessonId, pythonLanguageId);
            
            return result.success && result.template && 
                   (result.template.starterCode || result.template.templateCode);
        } catch (error) {
            console.log('No code exercise found for lesson:', lessonId);
            return false;
        }
    }

    async loadCodeTemplate(lessonId, languageId) {
        try {
            const result = await this.api.getCodeTemplate(lessonId, languageId);
            
            if (result.success && result.template) {
                const codeEditor = document.getElementById('code-editor');
                if (codeEditor) {
                    codeEditor.value = result.template.starterCode || result.template.templateCode || '';
                    codeEditor.disabled = !this.isUserEnrolled;
                }
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
        this.uiManager.showToast('Решение отправлено на проверку!', 'success');
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
        document.getElementById('language-filter').value = '';
        
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