export class TeacherCourseCreator {
    constructor(apiService, uiManager) {
        console.log('📝 TeacherCourseCreator: конструктор');
        this.api = apiService;
        this.uiManager = uiManager;
        this.currentCourseId = null;
        this.currentLessonId = null;
        this.modules = [];
        this.currentCourseData = null;
        this.baseUrl = 'https://localhost:7000';
        this.cachedTestCases = [];
        this.cachedQuizData = null;
        this.languages = []; 
    }

    initialize() {
        console.log('📝 TeacherCourseCreator: initialize()');
        this.setupEventListeners();
    }

    setupEventListeners() {
        console.log('📝 TeacherCourseCreator: setupEventListeners()');
        
        const createBtn = document.getElementById('create-course-btn');
        if (createBtn) {
            createBtn.addEventListener('click', (e) => {
                console.log('🟢 create-course-btn НАЖАТ!');
                e.preventDefault();
                this.showCourseCreationModal();
            });
        }

        document.addEventListener('click', (e) => {
            const target = e.target;
            
            if (target.id === 'add-module-btn') {
                console.log('🟢 add-module-btn НАЖАТА!');
                e.preventDefault();
                e.stopPropagation();
                this.addModule();
                return;
            }
            
            if (target.classList.contains('remove-module-btn')) {
                console.log('🟢 remove-module-btn НАЖАТА');
                e.preventDefault();
                const moduleCard = target.closest('.module-card');
                if (moduleCard) {
                    moduleCard.remove();
                    this.updateModuleIndices();
                }
                return;
            }
            
            if (target.classList.contains('add-lesson-btn')) {
                console.log('🟢 add-lesson-btn НАЖАТА');
                e.preventDefault();
                const moduleCard = target.closest('.module-card');
                if (moduleCard) {
                    this.addLessonToModule(moduleCard);
                }
                return;
            }
            
            if (target.classList.contains('remove-lesson-btn')) {
                console.log('🟢 remove-lesson-btn НАЖАТА');
                e.preventDefault();
                const lessonRow = target.closest('.lesson-row');
                if (lessonRow) {
                    lessonRow.remove();
                }
                return;
            }

            if (target.id === 'save-course-template') {
                console.log('🟢 save-course-template НАЖАТА');
                e.preventDefault();
                this.saveCourseTemplate();
                return;
            }

            if (target.id === 'publish-course-btn') {
                e.preventDefault();
                this.publishCourse();
                return;
            }

            if (target.id === 'add-test-case') {
                e.preventDefault();
                this.addTestCase();
                return;
            }

            if (target.classList.contains('remove-test')) {
                e.preventDefault();
                const testCase = target.closest('.test-case');
                if (testCase) {
                    testCase.remove();
                    this.updateTestNumbers();
                }
                return;
            }

            if (target.classList.contains('edit-lesson-btn')) {
                e.preventDefault();
                const courseId = target.dataset.courseId;
                const lessonId = target.dataset.lessonId;
                const hasQuiz = target.dataset.hasQuiz === 'true';
                const hasCode = target.dataset.hasCode === 'true';
                this.openLessonEditor(courseId, lessonId, hasQuiz, hasCode);
                return;
            }

            if (target.id === 'save-lesson-btn') {
                e.preventDefault();
                this.saveLessonContent();
                return;
            }

            if (target.classList.contains('tab-btn')) {
                e.preventDefault();
                const tabName = target.dataset.tab;
                this.switchTab(tabName);
                return;
            }
        });

        document.addEventListener('change', (e) => {
            const target = e.target;
            
            if (target.classList.contains('module-title-input')) {
                console.log('📝 изменение названия модуля:', target.value);
                return;
            }
            
            if (target.classList.contains('lesson-title-input')) {
                console.log('📝 изменение названия урока:', target.value);
                return;
            }
            
            if (target.classList.contains('lesson-quiz-checkbox')) {
                console.log('📝 изменение чекбокса теста:', target.checked);
                return;
            }
            
            if (target.classList.contains('lesson-code-checkbox')) {
                console.log('📝 изменение чекбокса кода:', target.checked);
                return;
            }
        });
    }

    async loadLanguages() {
        try {
            console.log('📚 Загрузка языков из БД...');
            const result = await this.api.getProgrammingLanguages();
            if (result.success) {
                this.languages = result.languages;
                console.log('✅ Языки загружены:', this.languages);
            } else {
                console.error('❌ Ошибка загрузки языков:', result.error);
                this.languages = [
                    { id: '11111111-1111-1111-1111-111111111111', name: 'python', monacoLanguageId: 'python' }
                ];
            }
        } catch (error) {
            console.error('❌ Ошибка загрузки языков:', error);
            this.languages = [
                { id: '11111111-1111-1111-1111-111111111111', name: 'python', monacoLanguageId: 'python' }
            ];
        }
    }

    createCourseLanguageSelector() {
        const container = document.createElement('div');
        container.className = 'form-group';
        container.style.marginBottom = '20px';
        
        const options = this.languages.map(lang => 
            `<option value="${lang.id}" data-monaco="${lang.monacoLanguageId}">
                ${lang.name}
            </option>`
        ).join('');
        
        container.innerHTML = `
            <label style="display: block; margin-bottom: 5px; font-weight: 500;">
                Язык программирования курса *
            </label>
            <select id="course-language" class="filter-select" style="width: 100%; padding: 8px;" required>
                <option value="">Выберите язык</option>
                ${options}
            </select>
            <p class="muted" style="font-size: 12px; margin-top: 5px;">
                Все кодовые задания в курсе будут на этом языке
            </p>
        `;
        
        return container;
    }

    async showCourseCreationModal() {
        console.log('📝 showCourseCreationModal() вызван');
        
        await this.loadLanguages();
        
        const modal = document.getElementById('modal-create-course');
        if (!modal) {
            console.error('❌ Модальное окно не найдено!');
            return;
        }
        
        this.modules = [];
        
        const titleInput = document.getElementById('course-title');
        const descInput = document.getElementById('course-description');
        const difficultySelect = document.getElementById('course-difficulty');
        
        if (titleInput) titleInput.value = '';
        if (descInput) descInput.value = '';
        if (difficultySelect) difficultySelect.value = 'beginner';
        
        this.createModulesContainer(modal);
        
        const modalContent = modal.querySelector('.modal-content');
        const difficultyGroup = document.getElementById('course-difficulty')?.closest('.form-group');
        
        if (modalContent && difficultyGroup) {
            if (!document.getElementById('course-language')) {
                const languageSelector = this.createCourseLanguageSelector();
                difficultyGroup.insertAdjacentElement('afterend', languageSelector);
            }
        }
        
        modal.classList.remove('hidden');
        console.log('✅ модальное окно открыто');
    }

    createModulesContainer(modal) {
        const modalContent = modal.querySelector('.modal-content');
        if (!modalContent) return;
        
        const oldContainer = document.getElementById('modules-list');
        if (oldContainer) oldContainer.remove();
        
        let modulesHeader = Array.from(modalContent.querySelectorAll('h4')).find(el => el.textContent.includes('Модули и уроки'));
        if (!modulesHeader) {
            modulesHeader = document.createElement('h4');
            modulesHeader.textContent = 'Модули и уроки';
            modulesHeader.style.margin = '20px 0 10px 0';
            
            const addModuleBtn = document.getElementById('add-module-btn');
            if (addModuleBtn) {
                addModuleBtn.parentNode.insertBefore(modulesHeader, addModuleBtn);
            } else {
                modalContent.appendChild(modulesHeader);
            }
        }
        
        const container = document.createElement('div');
        container.id = 'modules-list';
        container.style.cssText = `
            display: block;
            border: 2px solid #4299e1;
            border-radius: 8px;
            padding: 20px;
            margin: 15px 0;
            min-height: 100px;
            background: #f0f9ff;
        `;
        
        modulesHeader.insertAdjacentElement('afterend', container);
        
        console.log('✅ Контейнер для модулей создан');
    }

    addModule() {
        console.log('🟢🟢🟢 addModule() ВЫЗВАН!');
        
        const container = document.getElementById('modules-list');
        if (!container) {
            console.error('❌ modules-list не найден');
            return;
        }
        
        const moduleCount = container.children.length + 1;
        
        const moduleDiv = document.createElement('div');
        moduleDiv.className = 'module-card';
        moduleDiv.style.cssText = `
            border: 3px solid #4299e1;
            border-radius: 8px;
            margin-bottom: 20px;
            background: white;
            display: block;
            width: 100%;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        `;
        
        moduleDiv.innerHTML = `
            <div style="
                background: #4299e1;
                color: white;
                padding: 15px;
                border-radius: 8px 8px 0 0;
                display: flex;
                justify-content: space-between;
                align-items: center;
            ">
                <h4 style="margin: 0;">Модуль ${moduleCount}</h4>
                <button 
                    type="button" 
                    class="remove-module-btn"
                    style="
                        background: white;
                        color: #4299e1;
                        border: none;
                        padding: 5px 15px;
                        border-radius: 5px;
                        cursor: pointer;
                        font-weight: bold;
                        font-size: 14px;
                    "
                >Удалить модуль</button>
            </div>
            
            <div style="padding: 20px;">
                <div style="margin-bottom: 20px;">
                    <label style="
                        display: block;
                        margin-bottom: 8px;
                        font-weight: 600;
                        color: #2d3748;
                        font-size: 14px;
                    ">Название модуля</label>
                    <input 
                        type="text" 
                        class="module-title-input"
                        placeholder="Введите название модуля"
                        style="
                            width: 100%;
                            padding: 10px;
                            border: 2px solid #e2e8f0;
                            border-radius: 5px;
                            font-size: 14px;
                            box-sizing: border-box;
                        "
                    >
                </div>
                
                <div class="lessons-container" style="margin-bottom: 15px;">
                    ${this.createLessonHtml(1)}
                </div>
                
                <button 
                    type="button" 
                    class="add-lesson-btn"
                    style="
                        background: #48bb78;
                        color: white;
                        border: none;
                        padding: 10px 20px;
                        border-radius: 5px;
                        cursor: pointer;
                        font-weight: 600;
                        width: 100%;
                        font-size: 14px;
                        margin-top: 10px;
                    "
                >+ Добавить урок</button>
            </div>
        `;
        
        container.appendChild(moduleDiv);
        console.log(`✅ Модуль ${moduleCount} добавлен, теперь модулей:`, container.children.length);
    }

    createLessonHtml(lessonNumber) {
        return `
            <div class="lesson-row" style="
                border: 2px solid #48bb78;
                border-radius: 5px;
                padding: 15px;
                margin-bottom: 10px;
                background: #f0fff4;
                position: relative;
            ">
                <div style="display: flex; gap: 15px; align-items: center; flex-wrap: wrap;">
                    <div style="flex: 2; min-width: 200px;">
                        <input 
                            type="text" 
                            class="lesson-title-input"
                            placeholder="Название урока"
                            style="
                                width: 100%;
                                padding: 8px;
                                border: 2px solid #e2e8f0;
                                border-radius: 5px;
                                font-size: 14px;
                                box-sizing: border-box;
                            "
                        >
                    </div>
                    <div style="display: flex; gap: 20px; align-items: center; flex-wrap: wrap;">
                        <label style="display: flex; align-items: center; gap: 5px; cursor: pointer;">
                            <input type="checkbox" class="lesson-quiz-checkbox" style="width: 16px; height: 16px;"> 
                            <span style="font-size: 14px;">📝 Тест</span>
                        </label>
                        <label style="display: flex; align-items: center; gap: 5px; cursor: pointer;">
                            <input type="checkbox" class="lesson-code-checkbox" style="width: 16px; height: 16px;"> 
                            <span style="font-size: 14px;">💻 Код</span>
                        </label>
                        <span style="color: #718096; font-size: 14px; display: flex; align-items: center;">
                            📖 Теория
                        </span>
                        <button 
                            type="button" 
                            class="remove-lesson-btn"
                            style="
                                background: #f56565;
                                color: white;
                                border: none;
                                padding: 5px 10px;
                                border-radius: 3px;
                                cursor: pointer;
                                font-size: 12px;
                                font-weight: bold;
                                margin-left: 10px;
                            "
                        >✕</button>
                    </div>
                </div>
            </div>
        `;
    }

    addLessonToModule(moduleCard) {
        console.log('📝 addLessonToModule() вызван');
        
        const lessonsContainer = moduleCard.querySelector('.lessons-container');
        if (!lessonsContainer) return;
        
        const lessonCount = lessonsContainer.children.length + 1;
        const lessonHtml = this.createLessonHtml(lessonCount);
        
        lessonsContainer.insertAdjacentHTML('beforeend', lessonHtml);
        console.log('✅ Урок добавлен');
    }

    updateModuleIndices() {
        const container = document.getElementById('modules-list');
        if (!container) return;
        
        const modules = container.querySelectorAll('.module-card');
        modules.forEach((module, index) => {
            const header = module.querySelector('h4');
            if (header) {
                header.textContent = `Модуль ${index + 1}`;
            }
        });
    }

    async saveCourseTemplate() {
        const title = document.getElementById('course-title')?.value;
        const languageId = document.getElementById('course-language')?.value;
        
        if (!title) {
            this.uiManager.showToast('Введите название курса', 'warning');
            return;
        }
        
        if (!languageId) {
            this.uiManager.showToast('Выберите язык программирования', 'warning');
            return;
        }

        const token = localStorage.getItem('authToken');
        console.log('Токен:', token);
        
        if (!token) {
            this.uiManager.showToast('Ошибка авторизации. Войдите заново.', 'error');
            return;
        }

        const modules = [];
        const container = document.getElementById('modules-list');
        if (!container) return;
        
        const moduleCards = container.querySelectorAll('.module-card');
        
        for (const moduleCard of moduleCards) {
            const moduleTitle = moduleCard.querySelector('.module-title-input')?.value || '';
            const lessons = [];
            
            const lessonRows = moduleCard.querySelectorAll('.lesson-row');
            for (const lessonRow of lessonRows) {
                const lessonTitle = lessonRow.querySelector('.lesson-title-input')?.value || '';
                const hasQuiz = lessonRow.querySelector('.lesson-quiz-checkbox')?.checked || false;
                const hasCode = lessonRow.querySelector('.lesson-code-checkbox')?.checked || false;
                
                lessons.push({
                    title: lessonTitle,
                    order: lessons.length + 1,
                    hasTheory: true,
                    hasQuiz: hasQuiz,
                    hasCode: hasCode
                });
            }
            
            modules.push({
                title: moduleTitle,
                order: modules.length + 1,
                lessonsCount: lessons.length,
                lessons: lessons
            });
        }

        const courseData = {
            title: title,
            description: document.getElementById('course-description')?.value || '',
            difficultyLevel: document.getElementById('course-difficulty')?.value || 'beginner',
            modulesCount: modules.length,
            modules: modules,
            programmingLanguageId: languageId
        };

        try {
            this.uiManager.showButtonLoading('save-course-template', true);
            
            const response = await fetch(`${this.baseUrl}/api/teacher-course/create-template`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${token}`
                },
                body: JSON.stringify(courseData)
            });

            const result = await response.json();
            
            if (result.success) {
                this.currentCourseId = result.course.courseId;
                console.log('✅ ID курса сохранен:', this.currentCourseId);
                this.uiManager.showToast('Черновик курса создан!', 'success');
                document.getElementById('modal-create-course').classList.add('hidden');
                await this.showCourseEditor(this.currentCourseId);
            } else {
                throw new Error(result.error || 'Ошибка создания курса');
            }
        } catch (error) {
            console.error('Ошибка:', error);
            this.uiManager.showToast(error.message, 'error');
        } finally {
            this.uiManager.showButtonLoading('save-course-template', false);
        }
    }

    async showCourseEditor(courseId) {
        console.log('📝 showCourseEditor для курса:', courseId);
        this.currentCourseId = courseId;
        
        try {
            const token = localStorage.getItem('authToken');
            const response = await fetch(`${this.baseUrl}/api/teacher-course/${courseId}/structure`, {
                headers: {
                    'Authorization': `Bearer ${token}`
                }
            });
            
            const result = await response.json();
            console.log('📥 Ответ от сервера:', result);
            
            if (result.success && result.structure) {
                this.currentCourseData = result.structure;
                this.renderCourseEditor(result.structure);
            } else {
                console.error('❌ Структура не получена:', result);
                this.uiManager.showToast('Ошибка загрузки структуры курса', 'error');
            }
        } catch (error) {
            console.error('❌ Ошибка загрузки структуры:', error);
            this.uiManager.showToast('Ошибка загрузки структуры курса', 'error');
        }
    }

    renderCourseEditor(structure) {
        console.log('📝 renderCourseEditor, структура:', structure);
        
        const modal = document.getElementById('modal-course-editor');
        if (!modal) {
            console.error('❌ modal-course-editor не найден');
            return;
        }
        
        if (!structure || !structure.course) {
            console.error('❌ Неверная структура:', structure);
            return;
        }
        
        document.getElementById('editor-course-title').textContent = structure.course.title || 'Без названия';
        
        const container = document.getElementById('course-structure-container');
        if (!container) {
            console.error('❌ course-structure-container не найден');
            return;
        }
        
        container.innerHTML = '';
        
        if (!structure.modules || structure.modules.length === 0) {
            container.innerHTML = '<p class="muted">В курсе нет модулей</p>';
            modal.classList.remove('hidden');
            return;
        }
        
        structure.modules.forEach((module) => {
            const moduleDiv = document.createElement('div');
            moduleDiv.className = 'editor-module';
            moduleDiv.style.cssText = 'border: 1px solid #e2e8f0; border-radius: 8px; margin-bottom: 20px; background: #f8fafc;';
            
            let moduleHtml = `
                <div style="padding: 15px; border-bottom: 1px solid #e2e8f0; background: #f1f5f9; border-radius: 8px 8px 0 0;">
                    <h4 style="margin: 0;">${module.title || 'Без названия'}</h4>
                </div>
                <div style="padding: 15px;">
            `;
            
            if (module.lessons && module.lessons.length > 0) {
                module.lessons.forEach((lesson) => {
                    let badges = '';
                    if (lesson.hasTheory) {
                        badges += '<span style="background: #64748b; color: white; padding: 3px 8px; border-radius: 12px; font-size: 12px; margin-right: 5px;">📖 Теория</span>';
                    }
                    if (lesson.hasQuiz) {
                        badges += '<span style="background: #3b82f6; color: white; padding: 3px 8px; border-radius: 12px; font-size: 12px; margin-right: 5px;">📝 Тест</span>';
                    }
                    if (lesson.hasCode) {
                        badges += '<span style="background: #10b981; color: white; padding: 3px 8px; border-radius: 12px; font-size: 12px; margin-right: 5px;">💻 Код</span>';
                    }
                    
                    moduleHtml += `
                        <div style="border: 1px solid #e2e8f0; border-radius: 5px; margin-bottom: 15px; background: white;">
                            <div style="padding: 10px 15px; background: #f8fafc; border-bottom: 1px solid #e2e8f0; display: flex; justify-content: space-between; align-items: center;">
                                <h5 style="margin: 0;">${lesson.title || 'Без названия'}</h5>
                                <div>${badges}</div>
                            </div>
                            <div style="padding: 15px;">
                                <button class="btn-secondary btn-sm edit-lesson-btn" 
                                    data-course-id="${structure.course.id}" 
                                    data-lesson-id="${lesson.id}" 
                                    data-has-quiz="${lesson.hasQuiz || false}" 
                                    data-has-code="${lesson.hasCode || false}">
                                    Редактировать урок
                                </button>
                            </div>
                        </div>
                    `;
                });
            } else {
                moduleHtml += '<p class="muted">Нет уроков</p>';
            }
            
            moduleHtml += '</div>';
            moduleDiv.innerHTML = moduleHtml;
            container.appendChild(moduleDiv);
        });
        
        const publishBtn = document.getElementById('publish-course-btn');
        if (publishBtn) {
            const newPublishBtn = publishBtn.cloneNode(true);
            publishBtn.parentNode.replaceChild(newPublishBtn, publishBtn);
            
            newPublishBtn.addEventListener('click', (e) => {
                e.preventDefault();
                console.log('📢 Кнопка опубликовать нажата');
                this.publishCourse();
            });
        }
        
        modal.classList.remove('hidden');
        console.log('✅ Редактор курса открыт');
    }

    async openLessonEditor(courseId, lessonId, hasQuiz, hasCode) {
        console.log('📝 Открытие редактора урока:', { courseId, lessonId, hasQuiz, hasCode });
        
        this.cachedTestCases = [];
        this.cachedQuizData = null;
        
        const modal = document.getElementById('modal-lesson-editor');
        if (!modal) return;
        
        document.getElementById('lesson-editor-course-id').value = courseId;
        document.getElementById('lesson-editor-lesson-id').value = lessonId;
        document.getElementById('lesson-editor-title').textContent = 'Редактирование урока';
        
        const theoryTabBtn = document.getElementById('tab-theory-btn');
        const quizTabBtn = document.getElementById('tab-quiz-btn');
        const codeTabBtn = document.getElementById('tab-code-btn');
        
        if (theoryTabBtn) theoryTabBtn.style.display = 'inline-block';
        if (quizTabBtn) quizTabBtn.style.display = hasQuiz ? 'inline-block' : 'none';
        if (codeTabBtn) codeTabBtn.style.display = hasCode ? 'inline-block' : 'none';
        
        if (hasQuiz) {
            this.switchTab('quiz');
        } else if (hasCode) {
            this.switchTab('code');
        } else {
            this.switchTab('theory');
        }
        
        document.getElementById('theory-content').value = '';
        document.getElementById('quiz-question').value = '';
        document.getElementById('quiz-option1').value = '';
        document.getElementById('quiz-option2').value = '';
        document.getElementById('quiz-option3').value = '';
        document.getElementById('quiz-option4').value = '';
        document.getElementById('quiz-correct').value = '1';
        document.getElementById('quiz-explanation').value = '';
        document.getElementById('code-description').value = '';
        document.getElementById('code-starter').value = 'def solution():\n    pass';
        document.getElementById('code-solution').value = '';
        
        const testsContainer = document.getElementById('test-cases-container');
        testsContainer.innerHTML = '';
        this.addTestCase();
        
        await this.loadLessonContent(courseId, lessonId);
        
        modal.classList.remove('hidden');
    }

    async loadLessonContent(courseId, lessonId) {
        console.log('📥 Загрузка контента урока:', lessonId);
        try {
            const token = localStorage.getItem('authToken');
            
            const theoryRes = await fetch(`${this.baseUrl}/api/teacher-course/${courseId}/lesson/${lessonId}/theory`, {
                headers: { 'Authorization': `Bearer ${token}` }
            });
            if (theoryRes.ok) {
                const theoryData = await theoryRes.json();
                if (theoryData.success) {
                    document.getElementById('theory-content').value = theoryData.content || '';
                }
            }
            
            const quizRes = await fetch(`${this.baseUrl}/api/teacher-course/${courseId}/lesson/${lessonId}/quiz`, {
                headers: { 'Authorization': `Bearer ${token}` }
            });
            if (quizRes.ok) {
                const quizData = await quizRes.json();
                if (quizData.success && quizData.quiz) {
                    document.getElementById('quiz-question').value = quizData.quiz.questionText || '';
                    document.getElementById('quiz-option1').value = quizData.quiz.option1 || '';
                    document.getElementById('quiz-option2').value = quizData.quiz.option2 || '';
                    document.getElementById('quiz-option3').value = quizData.quiz.option3 || '';
                    document.getElementById('quiz-option4').value = quizData.quiz.option4 || '';
                    document.getElementById('quiz-correct').value = quizData.quiz.correctOption || '1';
                    document.getElementById('quiz-explanation').value = quizData.quiz.explanation || '';
                }
            }
            
            const codeRes = await fetch(`${this.baseUrl}/api/teacher-course/${courseId}/lesson/${lessonId}/code`, {
                headers: { 'Authorization': `Bearer ${token}` }
            });
            if (codeRes.ok) {
                const codeData = await codeRes.json();
                if (codeData.success && codeData.code) {
                    document.getElementById('code-description').value = codeData.code.taskDescription || '';
                    document.getElementById('code-starter').value = codeData.code.starterCode || 'def solution():\n    pass';
                    document.getElementById('code-solution').value = codeData.code.solutionCode || '';
                    
                    const testsContainer = document.getElementById('test-cases-container');
                    testsContainer.innerHTML = '';
                    if (codeData.code.testCases && codeData.code.testCases.length > 0) {
                        codeData.code.testCases.forEach(test => {
                            this.addTestCase(test.input, test.expectedOutput, test.isHidden);
                        });
                    } else {
                        this.addTestCase();
                    }
                }
            }
            
        } catch (error) {
            console.error('❌ Ошибка загрузки контента:', error);
        }
    }

    switchTab(tabName) {
        const codeTab = document.getElementById('code-tab');
        if (codeTab && !codeTab.classList.contains('hidden')) {
            this.cacheCurrentTestCases();
        }
        
        const quizTab = document.getElementById('quiz-tab');
        if (quizTab && !quizTab.classList.contains('hidden')) {
            this.cacheCurrentQuizData();
        }
        
        document.querySelectorAll('.tab-btn').forEach(btn => {
            btn.classList.remove('active');
        });
        
        document.querySelectorAll('.tab-content').forEach(content => {
            content.classList.add('hidden');
        });
        
        const activeBtn = document.querySelector(`[data-tab="${tabName}"]`);
        if (activeBtn) activeBtn.classList.add('active');
        
        const activeTab = document.getElementById(`${tabName}-tab`);
        if (activeTab) activeTab.classList.remove('hidden');
        
        if (tabName === 'code') {
            this.restoreCachedTestCases();
        }
        
        if (tabName === 'quiz') {
            this.restoreCachedQuizData();
        }
    }

    cacheCurrentTestCases() {
        this.cachedTestCases = [];
        const testElements = document.querySelectorAll('.test-case');
        
        testElements.forEach(test => {
            const input = test.querySelector('.test-input')?.value || '';
            const output = test.querySelector('.test-output')?.value || '';
            const isHidden = test.querySelector('.test-hidden')?.checked || false;
            
            this.cachedTestCases.push({
                input,
                output,
                isHidden
            });
        });
        
        console.log('💾 Тесты сохранены в кэш:', this.cachedTestCases);
    }

    restoreCachedTestCases() {
        const container = document.getElementById('test-cases-container');
        if (!container) return;
        
        container.innerHTML = '';
        
        if (this.cachedTestCases.length > 0) {
            this.cachedTestCases.forEach(test => {
                this.addTestCase(test.input, test.output, test.isHidden);
            });
        } else {
            this.addTestCase();
        }
        
        console.log('📋 Тесты восстановлены из кэша:', this.cachedTestCases);
    }

    cacheCurrentQuizData() {
        this.cachedQuizData = {
            questionText: document.getElementById('quiz-question').value,
            option1: document.getElementById('quiz-option1').value,
            option2: document.getElementById('quiz-option2').value,
            option3: document.getElementById('quiz-option3').value,
            option4: document.getElementById('quiz-option4').value,
            correctOption: document.getElementById('quiz-correct').value,
            explanation: document.getElementById('quiz-explanation').value
        };
        
        console.log('💾 Данные теста сохранены в кэш:', this.cachedQuizData);
    }

    restoreCachedQuizData() {
        if (!this.cachedQuizData) return;
        
        document.getElementById('quiz-question').value = this.cachedQuizData.questionText || '';
        document.getElementById('quiz-option1').value = this.cachedQuizData.option1 || '';
        document.getElementById('quiz-option2').value = this.cachedQuizData.option2 || '';
        document.getElementById('quiz-option3').value = this.cachedQuizData.option3 || '';
        document.getElementById('quiz-option4').value = this.cachedQuizData.option4 || '';
        document.getElementById('quiz-correct').value = this.cachedQuizData.correctOption || '1';
        document.getElementById('quiz-explanation').value = this.cachedQuizData.explanation || '';
        
        console.log('📋 Данные теста восстановлены из кэша');
    }

    addTestCase(input = '', output = '', isHidden = false) {
        const container = document.getElementById('test-cases-container');
        
        const testDiv = document.createElement('div');
        testDiv.className = 'test-case';
        testDiv.style.cssText = 'border: 1px solid #e2e8f0; border-radius: 5px; padding: 15px; margin-bottom: 15px; background: #f8fafc;';
        
        const testCount = container.children.length + 1;
        
        testDiv.innerHTML = `
            <div style="display: flex; justify-content: space-between; margin-bottom: 10px;">
                <h5 style="margin: 0;">Тест ${testCount}</h5>
                <button type="button" class="btn-danger btn-xs remove-test">✕</button>
            </div>
            <div class="form-group" style="margin-bottom: 10px;">
                <label style="display: block; margin-bottom: 3px; font-size: 13px;">Входные данные:</label>
                <input type="text" class="test-input form-input" value="${input}" placeholder="например: 5 10" style="width: 100%; padding: 6px; border: 1px solid #e2e8f0; border-radius: 4px;">
            </div>
            <div class="form-group" style="margin-bottom: 10px;">
                <label style="display: block; margin-bottom: 3px; font-size: 13px;">Ожидаемый вывод:</label>
                <input type="text" class="test-output form-input" value="${output}" placeholder="например: 15" style="width: 100%; padding: 6px; border: 1px solid #e2e8f0; border-radius: 4px;">
            </div>
            <label style="display: flex; align-items: center; gap: 5px; cursor: pointer;">
                <input type="checkbox" class="test-hidden" ${isHidden ? 'checked' : ''}>
                <span style="font-size: 13px;">Скрытый тест (не показывать студенту)</span>
            </label>
        `;
        
        container.appendChild(testDiv);
        this.updateTestNumbers();
    }

    updateTestNumbers() {
        const tests = document.querySelectorAll('.test-case');
        tests.forEach((test, index) => {
            const title = test.querySelector('h5');
            if (title) title.textContent = `Тест ${index + 1}`;
        });
    }

    async saveLessonContent() {
        console.log('💾 Сохранение урока');
        
        const courseId = document.getElementById('lesson-editor-course-id').value;
        const lessonId = document.getElementById('lesson-editor-lesson-id').value;
        const token = localStorage.getItem('authToken');
        
        const theoryContent = document.getElementById('theory-content').value;
        if (theoryContent) {
            await fetch(`${this.baseUrl}/api/teacher-course/${courseId}/lesson/${lessonId}/theory`, {
                method: 'PUT',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${token}`
                },
                body: JSON.stringify({ content: theoryContent })
            });
        }
        
        const quizTabBtn = document.getElementById('tab-quiz-btn');
        if (quizTabBtn && quizTabBtn.style.display !== 'none') {
            const quizData = {
                questionText: document.getElementById('quiz-question').value,
                option1: document.getElementById('quiz-option1').value,
                option2: document.getElementById('quiz-option2').value,
                option3: document.getElementById('quiz-option3').value,
                option4: document.getElementById('quiz-option4').value,
                correctOption: parseInt(document.getElementById('quiz-correct').value),
                explanation: document.getElementById('quiz-explanation').value
            };
            
            await fetch(`${this.baseUrl}/api/teacher-course/${courseId}/lesson/${lessonId}/quiz`, {
                method: 'PUT',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${token}`
                },
                body: JSON.stringify(quizData)
            });
        }
        
        const codeTabBtn = document.getElementById('tab-code-btn');
        if (codeTabBtn && codeTabBtn.style.display !== 'none') {
            const testCases = [];
            document.querySelectorAll('.test-case').forEach(test => {
                const input = test.querySelector('.test-input')?.value || '';
                const output = test.querySelector('.test-output')?.value || '';
                const isHidden = test.querySelector('.test-hidden')?.checked || false;
                
                if (output.trim() !== '') {
                    testCases.push({
                        input: input,
                        expectedOutput: output,
                        isHidden: isHidden
                    });
                    console.log(`📝 Добавлен тест: input="${input}", output="${output}"`);
                }
            });
            
            console.log(`📊 Всего тестов для отправки: ${testCases.length}`);
            
            const codeData = {
                taskDescription: document.getElementById('code-description').value || '',
                starterCode: document.getElementById('code-starter').value || '',
                solutionCode: document.getElementById('code-solution').value || '',
                testCases: testCases
            };
            
            await fetch(`${this.baseUrl}/api/teacher-course/${courseId}/lesson/${lessonId}/code`, {
                method: 'PUT',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${token}`
                },
                body: JSON.stringify(codeData)
            });
        }
        
        this.uiManager.showToast('Урок сохранен', 'success');
        document.getElementById('modal-lesson-editor').classList.add('hidden');
    }

    async publishCourse() {
        console.log('📢 Кнопка опубликовать нажата');
        
        if (!this.currentCourseId) {
            console.error('❌ Нет ID курса');
            this.uiManager.showToast('Нет ID курса', 'error');
            return;
        }
        
        if (!confirm('Опубликовать курс? После публикации курс станет доступен для студентов.')) return;
        
        try {
            const token = localStorage.getItem('authToken');
            console.log('📤 Отправка запроса на публикацию:', {
                courseId: this.currentCourseId,
                url: `${this.baseUrl}/api/teacher-course/${this.currentCourseId}/publish`
            });
            
            const response = await fetch(`${this.baseUrl}/api/teacher-course/${this.currentCourseId}/publish`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${token}`
                },
                body: JSON.stringify({ courseId: this.currentCourseId })
            });

            console.log('📥 Статус ответа:', response.status);
            
            const responseText = await response.text();
            console.log('📥 Текст ответа:', responseText);
            
            let result;
            try {
                result = JSON.parse(responseText);
            } catch (e) {
                console.error('❌ Ответ не JSON:', responseText);
                throw new Error('Сервер вернул некорректный ответ');
            }
            
            if (result.success) {
                this.uiManager.showToast('Курс успешно опубликован!', 'success');
                document.getElementById('modal-course-editor').classList.add('hidden');
                
                if (window.app && window.app.teacherManager) {
                    window.app.teacherManager.loadTeacherCourses();
                }
            } else {
                throw new Error(result.error || 'Ошибка публикации');
            }
        } catch (error) {
            console.error('❌ Ошибка:', error);
            this.uiManager.showToast(error.message, 'error');
        }
    }
}