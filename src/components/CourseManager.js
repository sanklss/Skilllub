import { ApiService } from '../services/ApiService.js';

export class CourseManager {
    constructor(apiService, uiManager, quizManager) {
        this.api = apiService;
        this.uiManager = uiManager;
        this.quizManager = quizManager;
        this.currentCourse = null;
        this.currentLesson = null;
        this.currentLessons = [];
        this.currentModule = null;
        this.allModules = [];
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
                
                if (result.lessons.length > 0) {
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

    async openLesson(lessonId) {
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
                
                if (hasCodeExercise) {
                    const pythonLanguageId = '11111111-1111-1111-1111-111111111111';
                    await this.loadCodeTemplate(lessonId, pythonLanguageId);
                    this.uiManager.showCodeSection();
                } else {
                    this.uiManager.hideCodeSection();
                }
                
                await this.quizManager.loadQuizQuestions(lessonId);
            }
        } catch (error) {
            console.error('Failed to open lesson:', error);
        }
    }

    async openLessonFromModule(lessonId, moduleId) {
        await this.loadCurrentModule(moduleId);
        await this.openLesson(lessonId);
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
                }
            }
        } catch (error) {
            console.error('Failed to load code template:', error);
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
                    lessonsHtml += `
                        <li class="lesson-item" data-lesson-id="${lesson.id}" 
                            onclick="app.courseManager.openLessonFromModule('${lesson.id}', '${moduleId}')">
                            <div class="lesson-icon">${lesson.order}</div>
                            <div class="lesson-info">
                                <div class="lesson-title">${lesson.title}</div>
                            </div>
                        </li>
                    `;
                });
                
                lessonsList.innerHTML = lessonsHtml;
            }
        } catch (error) {
            console.error('Failed to load lessons for sidebar:', error);
        }
    }

    renderLessonsSidebar(lessons) {
        // Можно оставить пустым или добавить дополнительную логику если нужно
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
            stepContent.innerHTML = `
                <h2>${lesson.title}</h2>
                <p class="lesson-description">${lesson.description || ''}</p>
                <div class="lesson-content">
                    ${lesson.content ? lesson.content.replace(/\n/g, '<br>') : 'Контент урока пока не добавлен.'}
                </div>
            `;
        }
    }

    goToPreviousStep() {
        if (!this.currentLesson || this.currentLessons.length === 0) return;
        
        const currentIndex = this.currentLessons.findIndex(lesson => lesson.id === this.currentLesson.id);
        if (currentIndex > 0) {
            const prevLesson = this.currentLessons[currentIndex - 1];
            
            // Находим модуль предыдущего урока
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
            
            // Находим модуль следующего урока
            const nextLessonModuleId = this.findModuleIdByLessonId(nextLesson.id);
            if (nextLessonModuleId) {
                this.openLessonFromModule(nextLesson.id, nextLessonModuleId);
            } else {
                this.openLesson(nextLesson.id);
            }
        }
    }

    findModuleIdByLessonId(lessonId) {
        // Ищем модуль, которому принадлежит урок
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

    runCode() {
        const code = document.getElementById('code-editor').value;
        const language = document.getElementById('language-select').value;
        
        console.log('Running code:', { code, language });
        document.getElementById('results-section').classList.remove('hidden');
    }

    resetCode() {
        document.getElementById('code-editor').value = '';
        document.getElementById('results-section').classList.add('hidden');
    }

    async submitCode() {
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