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
    
    const submitBtn = document.getElementById('submit-code');
    if (submitBtn) {
        console.log('✅ Found submit-code button, adding event listener');
        
        const newSubmitBtn = submitBtn.cloneNode(true);
        submitBtn.parentNode.replaceChild(newSubmitBtn, submitBtn);
        
        newSubmitBtn.addEventListener('click', (e) => {
            console.log('🟢 submit-code button clicked!');
            this.submitCode();
        });
    }
    
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
        
        if (this.currentLessons && this.currentLessons.length > 0) {
            console.log(`✅ Загружено ${this.currentLessons.length} уроков для модуля ${moduleId}`);
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
            
            if (this.isUserEnrolled) {
                console.log('📖 Отмечаем теорию как прочитанную для урока:', lessonId);
                await this.api.markTheoryAsRead(lessonId);
            }
            
            const pythonLanguageId = '11111111-1111-1111-1111-111111111111';
            const templateResult = await this.api.getCodeTemplate(lessonId, pythonLanguageId, this.userId);
            
            console.log('🔍 templateResult:', templateResult);

            if (templateResult.success && templateResult.template) {
                console.log('✅ template найден:', templateResult.template);
                console.log('📝 templateCode:', templateResult.template.templateCode);
                
                this.currentLesson.templateCode = templateResult.template.templateCode;
                console.log('✅ Загружен templateCode:', this.currentLesson.templateCode);
            } else {
                console.log('❌ template не найден или ошибка');
                this.currentLesson.templateCode = '';
            }
            
            this.renderLessonContent(this.currentLesson);
            
            await this.refreshLessonStatus(lessonId);
            
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

    const templateCode = lesson.templateCode || '';
    
    const oldTaskBlock = document.querySelector('.practice-task');
    if (oldTaskBlock) {
        oldTaskBlock.remove();
    }

    if (templateCode) {
        const codeSection = document.querySelector('.code-section');
        if (codeSection) {
            const taskBlock = document.createElement('div');
            taskBlock.className = 'practice-task';
            taskBlock.style.cssText = `
                background: #fff3cd;
                padding: 20px;
                margin: 20px 0;
                border-left: 5px solid #ffc107;
                border-radius: 0 5px 5px 0;
            `;
            
            taskBlock.innerHTML = `
                <h3 style="color: #856404; margin: 0 0 10px 0;">📝 Практическое задание:</h3>
                <div style="color: #333; font-size: 15px; white-space: pre-line;">
                    ${templateCode.replace(/\n/g, '<br>')}
                </div>
            `;
            
            codeSection.parentNode.insertBefore(taskBlock, codeSection);
            console.log('✅ Блок с заданием вставлен перед code-section');
        }
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

    async runCode() {
    if (!this.isUserEnrolled) {
        this.uiManager.showToast('Запишитесь на курс, чтобы выполнять задания', 'warning');
        return;
    }
    
    const code = document.getElementById('code-editor').value;
    const language = document.getElementById('language-select').value;
    const inputData = document.getElementById('input-data')?.value || '';
    
    console.log('Запуск кода:', code);
    console.log('Входные данные:', inputData);
    
    if (!code || code.trim() === '') {
        this.uiManager.showToast('Напишите код перед запуском', 'warning');
        return;
    }
    
    try {
        this.uiManager.showButtonLoading('run-code', true);
        
        const result = await this.api.runCode(code, language, inputData);
        
        console.log('Результат выполнения:', result);
        
        this.showTestResults(result);
        
        this.uiManager.showButtonLoading('run-code', false);
        
    } catch (error) {
        console.error('Ошибка при выполнении кода:', error);
        this.uiManager.showToast('Ошибка при выполнении кода', 'error');
        this.uiManager.showButtonLoading('run-code', false);
    }
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
    console.log('🔵 submitCode called!');
    
    if (!this.isUserEnrolled) {
        console.log('🔴 User not enrolled');
        this.uiManager.showToast('Запишитесь на курс, чтобы выполнять задания', 'warning');
        return;
    }
    
    const codeEditor = document.getElementById('code-editor');
    if (!codeEditor) {
        console.log('🔴 Code editor not found');
        this.uiManager.showToast('Ошибка: редактор кода не найден', 'error');
        return;
    }
    
    const code = codeEditor.value;
    const language = document.getElementById('language-select')?.value || 'python';
    
    const inputData = document.getElementById('input-data')?.value || '';
    console.log('📝 Input data:', inputData);
    
    console.log('📝 Code to submit:', { 
        lessonId: this.currentLesson?.id,
        language,
        inputLength: inputData.length,
        codeLength: code.length,
        codePreview: code.substring(0, 50) + '...'
    });
    
    if (!this.currentLesson) {
        console.log('🔴 No current lesson');
        this.uiManager.showToast('Урок не выбран', 'error');
        return;
    }
    
    if (!code || code.trim() === '') {
        console.log('🔴 Empty code');
        this.uiManager.showToast('Напишите код перед отправкой', 'warning');
        return;
    }
    
    try {
        this.uiManager.showButtonLoading('submit-code', true);
        
        console.log('🟡 Calling API.runCodeTests...');
        
        const result = await this.api.runCodeTests(
            this.currentLesson.id, 
            code, 
            language,
            inputData  
        );
        
        console.log('🟢 API response received:', result);
        
        this.uiManager.showButtonLoading('submit-code', false);
        
        if (result.success && result.result) {
            console.log('✅ Result:', result.result);
            
            this.showTestResults(result.result);
            
            if (result.result.passedTests === result.result.totalTests && 
                result.result.totalTests > 0) {
                
                this.uiManager.showToast('🎉 Задание выполнено! Урок завершен.', 'success');
                
                await this.refreshLessonStatus(this.currentLesson.id);
                
                await this.checkAndUpdateModuleCompletion();
            } else {
                this.uiManager.showToast(`❌ Пройдено ${result.result.passedTests || 0} из ${result.result.totalTests || 0} тестов`, 'warning');
            }
        } else {
            console.log('🔴 API returned error:', result.error);
            this.uiManager.showToast(result.error || 'Ошибка при проверке кода', 'error');
        }
    } catch (error) {
        console.error('🔴 Exception in submitCode:', error);
        this.uiManager.showToast('Ошибка: ' + error.message, 'error');
        this.uiManager.showButtonLoading('submit-code', false);
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
        
        console.log('🔄 Обновление прогресса для курса:', courseId);
        
        const enrollmentResult = await this.api.checkEnrollment(courseId);
        
        if (enrollmentResult.success && enrollmentResult.isEnrolled) {
            this.courseProgress = enrollmentResult.progress || 0;
            console.log('Прогресс обновлен:', this.courseProgress);
                        const sidebarProgressBar = document.querySelector('.course-sidebar .progress-fill');
            const sidebarProgressText = document.querySelector('.course-sidebar .progress-text');
            
            if (sidebarProgressBar) {
                sidebarProgressBar.style.width = `${this.courseProgress}%`;
                console.log('Сайдбар прогресс обновлен');
            }
            
            if (sidebarProgressText) {
                sidebarProgressText.textContent = `${this.courseProgress}%`;
            }
            
            const largeProgressBar = document.querySelector('.progress-bar-large .progress-fill');
            const largeProgressText = document.querySelector('.course-progress-display h3');
            
            if (largeProgressBar) {
                largeProgressBar.style.width = `${this.courseProgress}%`;
            }
            
            if (largeProgressText) {
                largeProgressText.textContent = `Ваш прогресс: ${this.courseProgress}%`;
            }
            
            console.log(`📊 Прогресс отображен: ${this.courseProgress}%`);
        }
    } catch (error) {
        console.error('❌ Ошибка:', error);
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

async loadCodeTemplate(lessonId) {
    const codeEditor = document.getElementById('code-editor');
    codeEditor.value = 'print("Hello World")';
    codeEditor.disabled = false;
    
    document.getElementById('run-code').disabled = false;
}
async onLessonOpened(lessonId) {
        try {
            if (!this.isUserEnrolled) {
                console.log('User not enrolled, skipping theory marking');
                return;
            }
            
            console.log('📖 Marking theory as read for lesson:', lessonId);
            
            const result = await this.api.markTheoryAsRead(lessonId);
            
            if (result.success) {
                console.log('✅ Theory marked as read:', lessonId);
                
                await this.refreshLessonStatus(lessonId);
                
                await this.checkLessonCompletion(lessonId);
            } else {
                console.warn('Failed to mark theory as read:', result.error);
            }
        } catch (error) {
            console.error('❌ Error marking theory as read:', error);
        }
    }

    async refreshLessonStatus(lessonId) {
    try {
        const result = await this.api.getLessonDetailedStatus(lessonId);
        
        if (result.success && result.status) {
            const status = result.status;
            
            console.log(`📊 Lesson ${lessonId} status:`, {
                theory: status.theoryCompleted,
                quiz: status.quizCompleted,
                code: status.codeCompleted,
                completed: status.isCompleted
            });
            
            if (this.currentLesson && this.currentLesson.id === lessonId) {
                this.currentLesson.isTheoryCompleted = status.theoryCompleted;
                this.currentLesson.isPracticeCompleted = status.quizCompleted || status.codeCompleted;
                this.currentLesson.isCompleted = status.isCompleted;
            }
            
            this.updateLessonStatusIcons(lessonId, status);
            
            return status;
        }
    } catch (error) {
        console.error('❌ Error refreshing lesson status:', error);
    }
}

    updateLessonStatusIcons(lessonId, status) {
    const lessonElement = document.querySelector(`[data-lesson-id="${lessonId}"]`);
    if (!lessonElement) return;

    lessonElement.classList.remove('theory-done', 'practice-done', 'completed', 'not-started', 'quiz-done', 'code-done');
    
    if (status.isCompleted) {
        lessonElement.classList.add('completed');
    } 
    else if (status.hasQuiz && status.hasCodeExercise) {
        if (status.quizCompleted && status.codeCompleted) {
            lessonElement.classList.add('completed');
        } else if (status.quizCompleted) {
            lessonElement.classList.add('quiz-done');
        } else if (status.codeCompleted) {
            lessonElement.classList.add('code-done');
        } else if (status.theoryCompleted) {
            lessonElement.classList.add('theory-done');
        } else {
            lessonElement.classList.add('not-started');
        }
    }
    else if (status.hasQuiz) {
        if (status.quizCompleted) {
            lessonElement.classList.add('completed');
        } else if (status.theoryCompleted) {
            lessonElement.classList.add('theory-done');
        } else {
            lessonElement.classList.add('not-started');
        }
    }
    else if (status.hasCodeExercise) {
        if (status.codeCompleted) {
            lessonElement.classList.add('completed');
        } else if (status.theoryCompleted) {
            lessonElement.classList.add('theory-done');
        } else {
            lessonElement.classList.add('not-started');
        }
    }
    else {
        if (status.theoryCompleted) {
            lessonElement.classList.add('completed');
        } else {
            lessonElement.classList.add('not-started');
        }
    }

    let statusElement = lessonElement.querySelector('.lesson-status');
    if (!statusElement) {
        statusElement = document.createElement('div');
        statusElement.className = 'lesson-status';
        const lessonInfo = lessonElement.querySelector('.lesson-info');
        if (lessonInfo) {
            lessonInfo.appendChild(statusElement);
        }
    }
    
    if (status.isCompleted) {
        statusElement.textContent = '✅ Завершен';
        statusElement.className = 'lesson-status completed';
    } else if (status.hasQuiz && status.hasCodeExercise) {
        if (status.quizCompleted && status.codeCompleted) {
            statusElement.textContent = '✅ Завершен';
            statusElement.className = 'lesson-status completed';
        } else if (status.quizCompleted) {
            statusElement.textContent = '📝 Тест пройден';
            statusElement.className = 'lesson-status quiz-done';
        } else if (status.codeCompleted) {
            statusElement.textContent = '💻 Код готов';
            statusElement.className = 'lesson-status code-done';
        } else if (status.theoryCompleted) {
            statusElement.textContent = '📖 Теория';
            statusElement.className = 'lesson-status theory';
        } else {
            statusElement.textContent = '📝 Не начат';
            statusElement.className = 'lesson-status not-started';
        }
    } else if (status.hasQuiz) {
        if (status.quizCompleted) {
            statusElement.textContent = '✅ Завершен';
            statusElement.className = 'lesson-status completed';
        } else if (status.theoryCompleted) {
            statusElement.textContent = '📖 Теория';
            statusElement.className = 'lesson-status theory';
        } else {
            statusElement.textContent = '📝 Не начат';
            statusElement.className = 'lesson-status not-started';
        }
    } else if (status.hasCodeExercise) {
        if (status.codeCompleted) {
            statusElement.textContent = '✅ Завершен';
            statusElement.className = 'lesson-status completed';
        } else if (status.theoryCompleted) {
            statusElement.textContent = '📖 Теория';
            statusElement.className = 'lesson-status theory';
        } else {
            statusElement.textContent = '📝 Не начат';
            statusElement.className = 'lesson-status not-started';
        }
    } else {
        if (status.theoryCompleted) {
            statusElement.textContent = '✅ Завершен';
            statusElement.className = 'lesson-status completed';
        } else {
            statusElement.textContent = '📝 Не начат';
            statusElement.className = 'lesson-status not-started';
        }
    }
}

    async checkLessonCompletion(lessonId) {
        try {
            const result = await this.api.getLessonDetailedStatus(lessonId);
            
            if (result.success && result.status && result.status.isCompleted) {
                console.log('🎉 Lesson fully completed:', lessonId);
                await this.onLessonCompleted(lessonId);
                return true;
            }
            
            return false;
        } catch (error) {
            console.error('❌ Error checking lesson completion:', error);
            return false;
        }
    }

    async onLessonCompleted(lessonId) {
        console.log('🎉 Lesson fully completed:', lessonId);
        
        this.uiManager.showToast('🎉 Урок полностью завершен!', 'success');
        
        if (this.currentCourse) {
            await this.updateCourseProgressInUI(this.currentCourse.id);
        }
        
        await this.checkAndUpdateModuleCompletion();
        
        if (this.currentModule) {
            await this.loadModuleLessons(this.currentModule.id);
        }
        
        if (this.currentLesson && this.currentLesson.id === lessonId) {
            this.currentLesson.isCompleted = true;
            
            this.showNextLessonPrompt();
        }
    }

    showNextLessonPrompt() {
        const currentIndex = this.currentLessons.findIndex(l => l.id === this.currentLesson?.id);
        
        if (currentIndex < this.currentLessons.length - 1) {
            const nextLesson = this.currentLessons[currentIndex + 1];
            
            const stepContent = document.querySelector('.step-content');
            if (stepContent && !stepContent.querySelector('.next-lesson-prompt')) {
                const promptHtml = `
                    <div class="next-lesson-prompt">
                        <div class="prompt-icon">🎉</div>
                        <div class="prompt-text">
                            <h4>Урок завершен!</h4>
                            <p>Хотите перейти к следующему уроку?</p>
                        </div>
                        <div class="prompt-actions">
                            <button class="btn-primary" onclick="app.courseManager.goToNextStep()">
                                Следующий урок →
                            </button>
                            <button class="btn-secondary" onclick="this.closest('.next-lesson-prompt').remove()">
                                Остаться здесь
                            </button>
                        </div>
                    </div>
                `;
                
                stepContent.insertAdjacentHTML('beforeend', promptHtml);
            }
        }
    }
    
    async completeLessonAutomatically(lessonId) {
        try {
            if (!this.isUserEnrolled) return;
            
            console.log('Автоматическое завершение урока (без заданий):', lessonId);
            
            await this.api.markTheoryAsRead(lessonId);
            await this.api.completeLesson(lessonId);
            
            await this.refreshLessonStatus(lessonId);
            
        } catch (error) {
            console.error('❌ Ошибка автоматического завершения урока:', error);
        }
    }

showTestResults(result) {
    console.log('📊 Showing test results:', result);
    
    let resultsSection = document.getElementById('results-section');
    
    if (!resultsSection) {
        console.log('Creating results section');
        resultsSection = document.createElement('div');
        resultsSection.id = 'results-section';
        resultsSection.className = 'results-section';
        
        const stepContainer = document.querySelector('.step-container');
        if (stepContainer) {
            stepContainer.appendChild(resultsSection);
        } else {
            console.log('Step container not found');
            return;
        }
    }
    
    resultsSection.classList.remove('hidden');
    
    let html = '<h3>Результаты проверки:</h3>';
    html += `<p>Пройдено тестов: ${result.passedTests || 0} из ${result.totalTests || 0}</p>`;
    html += `<p>Оценка: ${result.score || 0}%</p>`;
    
    if (result.output) {
        html += `<div class="test-output"><pre>${result.output}</pre></div>`;
    }
    
    if (result.testResults && result.testResults.length > 0) {
        html += '<div class="test-details"><h4>Детали:</h4>';
        result.testResults.forEach((test, index) => {
            const status = test.passed ? '✅' : '❌';
            html += `<div class="test-item ${test.passed ? 'passed' : 'failed'}">`;
            html += `<div>${status} Тест ${index + 1}</div>`;
            if (!test.passed && !test.isHidden && test.actualOutput) {
                html += `<div><small>Ожидалось: ${test.expectedOutput}</small></div>`;
                html += `<div><small>Получено: ${test.actualOutput}</small></div>`;
            }
            html += '</div>';
        });
        html += '</div>';
    }
    
    resultsSection.innerHTML = html;
    console.log('✅ Results displayed');
}

}