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

           
            await this.loadCourseModules(courseId);

            this.renderCourseAccessControls();

        } catch (error) {
            console.error('Failed to open course:', error);
            this.uiManager.showToast('Ошибка загрузки курса', 'error');
            this.uiManager.showSection('catalog');
        }
    }

    async loadCourseModules(courseId) {
        try {
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
        } catch (error) {
            console.error('Ошибка загрузки модулей:', error);
            this.allModules = [];
            this.renderCourseSidebar(this.currentCourse, []);
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
        if (!this.isUserEnrolled && this.isAuthenticated) {
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
                
                await this.checkAndUpdateLessonStatus(lessonId);
                
                const hasQuiz = await this.checkIfLessonHasQuiz(lessonId);
                const hasCodeExercise = await this.checkIfLessonHasCodeExercise(lessonId);
                
                console.log(`Урок ${lessonId}: квиз=${hasQuiz}, код=${hasCodeExercise}`);
                
                if (!hasQuiz && !hasCodeExercise) {
                    console.log('Урок без заданий - автоматически завершаем...');
                    await this.completeLessonAutomatically(lessonId);
                } else {
                    if (hasQuiz) {
                        const quizLoaded = await this.quizManager.loadQuizQuestions(lessonId);
                        if (quizLoaded) {
                            this.uiManager.showQuizSection();
                        } else {
                            this.uiManager.hideQuizSection();
                        }
                    } else {
                        this.uiManager.hideQuizSection();
                    }
                    
                    if (hasCodeExercise) {
                        const pythonLanguageId = '11111111-1111-1111-1111-111111111111';
                        await this.loadCodeTemplate(lessonId, pythonLanguageId);
                        this.uiManager.showCodeSection();
                    } else {
                        this.uiManager.hideCodeSection();
                    }
                }
                
            } else {
                this.uiManager.showToast('Урок не найден или недоступен', 'error');
            }
        } catch (error) {
            console.error('Failed to open lesson:', error);
            this.uiManager.showToast('Ошибка загрузки урока', 'error');
        }
    }

    async completeLessonAutomatically(lessonId) {
        try {
            if (!this.isUserEnrolled) return;
            
            console.log('Автоматическое завершение урока:', lessonId);
            
            this.updateLessonStatusInUI(lessonId, true);
            this.updateSidebarLessonStatus(lessonId, true);
            
            const result = await this.api.completeLesson(lessonId);
            
            if (result.success) {
                console.log('Урок автоматически завершен:', lessonId);
                this.uiManager.showToast('Урок пройден!', 'success');
                
                if (this.currentCourse) {
                    await this.updateCourseProgressInUI(this.currentCourse.id);
                }
                
                await this.checkAndUpdateModuleCompletion();
                
                this.uiManager.hideQuizSection();
                this.uiManager.hideCodeSection();
                
                this.showLessonCompletionMessage();
            } else {
                console.warn('Не удалось автоматически завершить урок:', result.error);
            }
        } catch (error) {
            console.error('Ошибка автоматического завершения урока:', error);
        }
    }

    async checkAndUpdateModuleCompletion() {
        try {
            if (!this.currentModule || !this.currentCourse || !this.userId) return;
            
            console.log('Проверка завершения модуля:', this.currentModule.id);
            
            
            const moduleLessons = this.currentLessons;
            const completedLessons = moduleLessons.filter(lesson => lesson.isCompleted);
            
            console.log(`Модуль ${this.currentModule.id}: завершено ${completedLessons.length}/${moduleLessons.length} уроков`);
            
            if (completedLessons.length >= moduleLessons.length && moduleLessons.length > 0) {
                console.log('Все уроки модуля завершены!');
                
                this.updateModuleStatusInUI(this.currentModule.id, true);
                
                await this.reloadCourseModules();
                
                this.uiManager.showToast('Модуль завершен! Следующий модуль разблокирован.', 'success');
            }
        } catch (error) {
            console.error('Ошибка проверки модуля:', error);
        }
    }

    async reloadCourseModules() {
        try {
            if (!this.currentCourse || !this.userId) return;
            
            console.log('Перезагрузка модулей курса:', this.currentCourse.id);
            
            const modulesResult = await this.api.getCourseModules(this.currentCourse.id, this.userId);
            
            if (modulesResult.success) {
                this.allModules = modulesResult.modules;
                this.renderCourseSidebar(this.currentCourse, this.allModules);
                console.log('Модули перезагружены:', this.allModules.length);
                
                this.showUnlockedModules();
            }
        } catch (error) {
            console.error('Ошибка перезагрузки модулей:', error);
        }
    }

    showUnlockedModules() {
        if (!this.allModules || this.allModules.length === 0) return;
        
        console.log('Проверка доступности модулей:');
        
        this.allModules.forEach((module, index) => {
            console.log(`Модуль ${index + 1}: ${module.title} - доступен: ${module.isAccessible}, завершен: ${module.isCompleted}`);
            
            if (module.isAccessible && !module.isCompleted) {
                console.log(`Модуль "${module.title}" доступен для изучения!`);
            }
        });
    }

    showLessonCompletionMessage() {
        const stepContent = document.querySelector('.step-content');
        if (stepContent && this.currentLesson) {
            stepContent.innerHTML += `
                <div class="lesson-completion-message">
                    <div class="completion-icon"></div>
                    <h3>Урок пройден!</h3>
                    <p>Вы успешно завершили урок "${this.currentLesson.title}"</p>
                    <div class="completion-actions">
                        ${this.currentLessons.length > 1 ? `
                            <button class="btn-primary" onclick="app.courseManager.goToNextStep()">
                                Следующий урок →
                            </button>
                        ` : ''}
                        <button class="btn-secondary" onclick="app.courseManager.openModule('${this.currentModule?.id}')">
                            Вернуться к модулю
                        </button>
                    </div>
                </div>
            `;
        }
    }

    async checkIfLessonHasQuiz(lessonId) {
        try {
            const result = await this.api.getQuizQuestions(lessonId);
            return result.success && result.questions && result.questions.length > 0;
        } catch (error) {
            console.log('No quiz found for lesson:', lessonId);
            return false;
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
                            <li class="loading">Загрузка уроков...</li>
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
        const result = await this.api.getModuleLessons(moduleId, this.userId);
        
        if (result.success) {
            const lessonsList = document.querySelector(`#lessons-${moduleId}`);
            if (!lessonsList) return;
            
            if (result.lessons.length === 0) {
                lessonsList.innerHTML = '<li class="muted">Уроки не найдены</li>';
                return;
            }
            
            let lessonsHtml = '';
            result.lessons.forEach(lesson => {
                let statusClass = '';
                let statusIcon = '';
                
                if (lesson.isCompleted) {
                    statusClass = 'completed';
                    statusIcon = '✓ ';
                } else if (lesson.hasQuiz) {
                    statusClass = '';
                    statusIcon = '';
                }
                
                lessonsHtml += `
                    <li class="lesson-item ${statusClass}" 
                        data-lesson-id="${lesson.id}"
                        onclick="app.courseManager.openLessonFromModule('${lesson.id}')">
                        <div class="lesson-icon">${lesson.order}</div>
                        <div class="lesson-info">
                            <div class="lesson-title">${statusIcon}${lesson.title}</div>
                            ${lesson.hasQuiz ? '<div class="lesson-quiz-indicator"></div>' : ''}
                        </div>
                    </li>
                `;
            });
            
            lessonsList.innerHTML = lessonsHtml;
        }
    } catch (error) {
        console.error('Error loading module lessons:', error);
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
        accessControls.innerHTML = '';
        accessControls.style.display = 'none';
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
                await this.loadCourseModules(this.currentCourse.id);
                this.renderCourseAccessControls();
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
    async checkAndUpdateModuleCompletion() {
    try {
        if (!this.currentModule || !this.currentCourse || !this.userId) {
            console.log('Невозможно проверить модуль: отсутствуют данные');
            return;
        }
        
        console.log('ПРОВЕРКА ЗАВЕРШЕНИЯ МОДУЛЯ:', this.currentModule.id);
        
        const modulesResult = await this.api.getCourseModules(this.currentCourse.id, this.userId);
        
        if (!modulesResult.success) {
            console.error('Не удалось загрузить модули');
            return;
        }
        
        const updatedCurrentModule = modulesResult.modules.find(m => m.id === this.currentModule.id);
        
        if (!updatedCurrentModule) {
            console.error('Текущий модуль не найден');
            return;
        }
        
        console.log(`Статус модуля "${updatedCurrentModule.title}":`, {
            isCompleted: updatedCurrentModule.isCompleted,
            isAccessible: updatedCurrentModule.isAccessible,
            order: updatedCurrentModule.order
        });
        
        this.updateModuleStatusInUI(updatedCurrentModule.id, updatedCurrentModule.isCompleted);
        
        if (updatedCurrentModule.isCompleted) {
            console.log('МОДУЛЬ ЗАВЕРШЕН! Перезагружаем список модулей...');
            
            this.uiManager.showToast(`Модуль "${updatedCurrentModule.title}" завершен!`, 'success');
            
            await this.reloadCourseModules();
            
            const nextModule = this.allModules.find(m => 
                m.isAccessible && !m.isCompleted && m.order > updatedCurrentModule.order
            );
            
            if (nextModule) {
                console.log(`Следующий модуль доступен: "${nextModule.title}"`);
                this.uiManager.showToast(`Модуль "${nextModule.title}" разблокирован!`, 'success');
            }
        } else {
            const completedLessons = this.currentLessons.filter(l => l.isCompleted).length;
            console.log(`Прогресс модуля: ${completedLessons}/${this.currentLessons.length} уроков`);
        }
        
    } catch (error) {
        console.error('Ошибка проверки модуля:', error);
    }
}

async reloadCourseModules() {
    try {
        if (!this.currentCourse || !this.userId) {
            console.log('Невозможно перезагрузить модули: нет данных');
            return;
        }
        
        console.log('Перезагрузка модулей курса:', this.currentCourse.id);
        
        const modulesResult = await this.api.getCourseModules(this.currentCourse.id, this.userId);
        
        if (modulesResult.success) {
            const oldModuleIds = this.allModules.map(m => m.id);
            
            this.allModules = modulesResult.modules;
            
            console.log('Модули после перезагрузки:');
            this.allModules.forEach((m, i) => {
                console.log(`  ${i+1}. ${m.title} - доступен: ${m.isAccessible}, завершен: ${m.isCompleted}`);
            });
            
            this.renderCourseSidebar(this.currentCourse, this.allModules);
            
            for (const module of this.allModules) {
                await this.loadModuleLessonsForSidebar(module.id);
            }
            
            if (this.currentModule) {
                const updatedCurrentModule = this.allModules.find(m => m.id === this.currentModule.id);
                if (updatedCurrentModule) {
                    this.currentModule = updatedCurrentModule;
                    this.updateModuleStatusInUI(this.currentModule.id, this.currentModule.isCompleted);
                }
            }
            
            console.log('Модули перезагружены');
            return true;
        } else {
            console.error('Ошибка перезагрузки модулей');
            return false;
        }
    } catch (error) {
        console.error('Ошибка перезагрузки модулей:', error);
        return false;
    }
}

async completeLessonAutomatically(lessonId) {
    try {
        if (!this.isUserEnrolled) {
            console.log('Пользователь не записан на курс');
            return;
        }
        
        console.log('Автоматическое завершение урока:', lessonId);
        
        const lesson = this.currentLessons.find(l => l.id === lessonId);
        if (lesson) {
            lesson.isCompleted = true;
            this.updateLessonStatusInUI(lessonId, true);
            this.updateSidebarLessonStatus(lessonId, true);
        }
        
        const result = await this.api.completeLesson(lessonId);
        
        if (result.success) {
            console.log('Урок успешно завершен на сервере');
            this.uiManager.showToast('Урок пройден!', 'success');
            
            if (this.currentCourse) {
                await this.updateCourseProgressInUI(this.currentCourse.id);
            }
            
            await this.checkAndUpdateModuleCompletion();
            
            if (this.currentModule) {
                await this.loadModuleLessons(this.currentModule.id);
            }
            
        } else {
            console.error('Не удалось завершить урок:', result.error);
        }
    } catch (error) {
        console.error('Ошибка автоматического завершения урока:', error);
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
                    
                  
                    this.updateLessonStatusInUI(this.currentLesson.id, true);
                    this.updateSidebarLessonStatus(this.currentLesson.id, true);
                    
                    
                    await this.checkAndUpdateModuleCompletion();
                    
                    
                    if (this.currentCourse) {
                        await this.updateCourseProgressInUI(this.currentCourse.id);
                    }
                }
            } catch (error) {
                console.error('Failed to complete lesson:', error);
                this.uiManager.showToast('Ошибка при завершении урока', 'error');
            }
        } else {
            this.uiManager.showToast('Решение отправлено на проверку!', 'success');
        }
    }

    async checkAndUpdateLessonStatus(lessonId) {
        try {
            if (!this.userId) return;
            
            const statusResult = await this.api.checkLessonProgress(lessonId);
            if (statusResult.success && statusResult.completed) {
                this.updateLessonStatusInUI(lessonId, true);
                console.log(`Урок ${lessonId} уже завершен`);
            }
        } catch (error) {
            console.error('Error checking lesson status:', error);
        }
    }

    async updateCourseProgressInUI(courseId) {
        try {
            if (!this.userId) return;
            
            const enrollmentResult = await this.api.checkEnrollment(courseId);
            if (enrollmentResult.success && enrollmentResult.isEnrolled) {
                this.courseProgress = enrollmentResult.progress || 0;
                
                const progressBar = document.querySelector('.progress-bar-large .progress-fill');
                const progressText = document.querySelector('.course-progress-display h3');
                
                if (progressBar) {
                    progressBar.style.width = `${this.courseProgress}%`;
                }
                if (progressText) {
                    progressText.textContent = `Ваш прогресс: ${this.courseProgress}%`;
                }
                
                console.log(`Прогресс курса обновлен: ${this.courseProgress}%`);
            }
        } catch (error) {
            console.error('Error updating course progress:', error);
        }
    }

    handleQuizCompleted(lessonId, isSuccess) {
        if (isSuccess) {
            this.updateLessonStatusInUI(lessonId, true);
            this.updateSidebarLessonStatus(lessonId, true);
            
            if (this.currentModule) {
                this.checkAndUpdateModuleCompletion();
            }
            
            if (this.currentCourse) {
                this.updateCourseProgressInUI(this.currentCourse.id);
            }
            
            this.uiManager.showToast('Тест пройден! Урок завершен.', 'success');
        }
    }

    updateLessonStatusInUI(lessonId, isCompleted) {
        const lessonElement = document.querySelector(`[data-lesson-id="${lessonId}"]`);
        if (lessonElement) {
            if (isCompleted) {
                lessonElement.classList.add('completed');
                const titleDiv = lessonElement.querySelector('.lesson-title');
                if (titleDiv && !titleDiv.textContent.includes('✓')) {
                    titleDiv.textContent = '✓ ' + titleDiv.textContent.replace('✓ ', '');
                }
            }
        }
    }

    updateSidebarLessonStatus(lessonId, isCompleted) {
    const lessonElement = document.querySelector(`[data-lesson-id="${lessonId}"]`);
    if (lessonElement) {
        if (isCompleted) {
            lessonElement.classList.add('completed');
            lessonElement.classList.remove('failed');
        } else {
            lessonElement.classList.remove('completed');
            lessonElement.classList.add('failed');
        }
    }
}

    updateModuleStatusInUI(moduleId, isCompleted) {
        const moduleElement = document.querySelector(`[data-module-id="${moduleId}"]`);
        if (moduleElement) {
            if (isCompleted) {
                moduleElement.classList.remove('accessible');
                moduleElement.classList.add('completed');
                
                const statusElement = moduleElement.querySelector('.module-status');
                if (statusElement) {
                    statusElement.textContent = '✓';
                }
            }
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