import { API_URL } from '../config/constants.js';
import { StorageService } from './StorageService.js';

export class ApiService {
    constructor() {
        this.storage = new StorageService();
        this.baseURL = API_URL;
    }

    async checkEnrollment(courseId) {
        const response = await this.request(`/progress/check-enrollment/${courseId}`);
        return response.json();
    }

    async enrollInCourse(courseId) {
        const response = await this.request('/progress/enroll', {
            method: 'POST',
            body: JSON.stringify({ courseId })
        });
        return response.json();
    }

    async getMyCourses() {
        const response = await this.request('/progress/my-courses');
        return response.json();
    }

    async completeLesson(lessonId) {
        const response = await this.request(`/progress/lesson/${lessonId}/complete`, {
            method: 'POST'
        });
        return response.json();
    }

    async getModuleStatus(moduleId) {
        const response = await this.request(`/progress/module/${moduleId}/status`);
        return response.json();
    }

    async request(endpoint, options = {}) {
        const url = `${this.baseURL}${endpoint}`;
        const config = {
            headers: {
                'Content-Type': 'application/json',
                ...options.headers
            },
            ...options
        };

        const token = this.storage.getAuthToken();
        if (token) {
            config.headers['Authorization'] = `Bearer ${token}`;
        }

        try {
            const response = await fetch(url, config);
            
            if (response.status === 401) {
                const refreshed = await this.refreshToken();
                if (refreshed) {
                    const newToken = this.storage.getAuthToken();
                    config.headers['Authorization'] = `Bearer ${newToken}`;
                    return await fetch(url, config);
                } else {
                    this.storage.clearAuth();
                    throw new Error('Сессия истекла');
                }
            }

            return response;
        } catch (error) {
            console.error('API Request failed:', error);
            throw error;
        }
    }

    async refreshToken() {
        try {
            const refreshToken = this.storage.getRefreshToken();
            if (!refreshToken) return false;

            const response = await fetch(`${this.baseURL}/auth/refresh-token`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    accessToken: this.storage.getAuthToken(),
                    refreshToken: refreshToken
                })
            });

            if (response.ok) {
                const data = await response.json();
                if (data.success) {
                    this.storage.setAuthToken(data.token);
                    this.storage.setRefreshToken(data.refreshToken);
                    return true;
                }
            }
            return false;
        } catch (error) {
            console.error('Token refresh failed:', error);
            return false;
        }
    }

    async login(email, password) {
        const response = await this.request('/auth/login', {
            method: 'POST',
            body: JSON.stringify({ Email: email, Password: password })
        });
        return response.json();
    }

    async register(userData) {
        const response = await this.request('/auth/register', {
            method: 'POST',
            body: JSON.stringify(userData)
        });
        return response.json();
    }

    async forgotPassword(email) {
        const response = await this.request('/auth/forgot-password', {
            method: 'POST',
            body: JSON.stringify({ Email: email })
        });
        return response.json();
    }

    async verifyResetCode(email, code) {
        const response = await this.request('/auth/verify-reset-code', {
            method: 'POST',
            body: JSON.stringify({ Email: email, Code: code })
        });
        return response.json();
    }

    async resetPassword(email, code, newPassword, confirmPassword) {
        const response = await this.request('/auth/reset-password', {
            method: 'POST',
            body: JSON.stringify({ 
                Email: email,
                Code: code,
                NewPassword: newPassword,
                ConfirmPassword: confirmPassword
            })
        });
        return response.json();
    }

    async validateToken() {
        const response = await this.request('/auth/validate-token', {
            method: 'POST'
        });
        return response.json();
    }

    async getCourses() {
        const response = await this.request('/courses');
        return response.json();
    }

async getCourse(courseId) {
    const response = await this.request(`/courses/${courseId}`);
    const data = await response.json();
    
    console.log('🔥🔥🔥 СЫРОЙ ОТВЕТ ОТ СЕРВЕРА:', JSON.stringify(data, null, 2));
    
    if (data.success && data.course) {
        console.log('🔥🔥🔥 ДАННЫЕ КУРСА ИЗ СЕРВЕРА:', JSON.stringify(data.course, null, 2));
        
        return {
            success: true,
            course: {
                id: data.course.id,
                title: data.course.title,
                description: data.course.description,
                difficultyLevel: data.course.difficultyLevel,
                isPublished: data.course.isPublished,
                createdBy: data.course.createdBy,
                programmingLanguageId: data.course.programmingLanguageId,
                programmingLanguageName: data.course.programmingLanguageName
            }
        };
    }
    return data;
}

    async getCourseModules(courseId, userId = null) {
        const url = userId 
            ? `/courses/${courseId}/modules?userId=${userId}`
            : `/courses/${courseId}/modules`;
        
        const response = await this.request(url);
        return response.json();
    }

    async getModuleLessons(moduleId, userId = null) {
    const url = userId 
        ? `/courses/modules/${moduleId}/lessons?userId=${userId}`
        : `/courses/modules/${moduleId}/lessons`;
    
    const response = await this.request(url);
    return response.json();
}

    async getLesson(lessonId, userId = null) {
        const url = userId 
            ? `/courses/lessons/${lessonId}?userId=${userId}`
            : `/courses/lessons/${lessonId}`;
        
        const response = await this.request(url);
        return response.json();
    }

    async getCodeTemplate(lessonId, languageId, userId = null) {
        const url = userId 
            ? `/courses/lessons/${lessonId}/code-template/${languageId}?userId=${userId}`
            : `/courses/lessons/${lessonId}/code-template/${languageId}`;
        
        const response = await this.request(url);
        return response.json();
    }

    async getQuizQuestions(lessonId) {
        const response = await this.request(`/quiz/lessons/${lessonId}/questions`);
        return response.json();
    }

    async submitQuiz(answers) {
        const response = await this.request('/quiz/submit-quiz', {
            method: 'POST',
            body: JSON.stringify({ answers })
        });
        return response.json();
    }

    async checkAnswer(questionId, userAnswer) {
        const response = await this.request('/quiz/check-answer', {
            method: 'POST',
            body: JSON.stringify({ questionId, userAnswer })
        });
        return response.json();
    }

    async getAdminUsers() {
        const response = await this.request('/admin/users');
        return response.json();
    }

    async updateUserRole(userId, role) {
        const response = await this.request(`/admin/users/${userId}/role`, {
            method: 'PUT',
            body: JSON.stringify({ role })
        });
        return response.json();
    }

    async getAdminStatistics() {
        const response = await this.request('/admin/statistics');
        return response.json();
    }

    async createCourse(courseData) {
        const response = await this.request('/admin/courses', {
            method: 'POST',
            body: JSON.stringify(courseData)
        });
        return response.json();
    }

    async deleteUser(userId) {
        try {
            const response = await this.request(`/admin/users/${userId}`, {
                method: 'DELETE'
            });
            
            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }
            
            return await response.json();
        } catch (error) {
            console.error('Delete user error:', error);
            return { success: false, error: error.message };
        }
    }

    async updateUser(userId, userData) {
        const response = await this.request(`/admin/users/${userId}`, {
            method: 'PUT',
            body: JSON.stringify(userData)
        });
        return response.json();
    }

    async changePassword(passwordData) {
        const response = await this.request('/auth/change-password', {
            method: 'POST',
            body: JSON.stringify(passwordData)
        });
        return response.json();
    }

    async updateUserPassword(userId, passwordData) {
        const response = await this.request(`/admin/users/${userId}/password`, {
            method: 'PUT',
            body: JSON.stringify(passwordData)
        });
        return response.json();
    }

    async revokeUserSessions(userId) {
        try {
            const response = await this.request(`/admin/users/${userId}/revoke-sessions`, {
                method: 'POST'
            });
            
            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }
            
            return await response.json();
        } catch (error) {
            console.error('Revoke sessions error:', error);
            return { success: false, error: error.message };
        }
    }

    async createUser(userData) {
        try {
            const response = await this.request('/admin/users', {
                method: 'POST',
                body: JSON.stringify(userData)
            });
            
            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }
            
            return await response.json();
        } catch (error) {
            console.error('Create user API error:', error);
            return { success: false, error: error.message };
        }
    }

    async initializeRoles() {
        const response = await this.request('/admin/init-roles', {
            method: 'POST'
        });
        return response.json();
    }

    async checkRoles() {
        const response = await this.request('/admin/check-roles');
        return response.json();
    }

    async checkUserRoles() {
        const response = await this.request('/admin/check-user-roles');
        return response.json();
    }

    async addTestUserRole() {
        const response = await this.request('/admin/add-test-user-role', {
            method: 'POST'
        });
        return response.json();
    }

    async cleanupExpiredTokens() {
        const response = await this.request('/admin/cleanup-expired-tokens', {
            method: 'POST'
        });
        return response.json();
    }
    
    async checkLessonProgress(lessonId) {
    try {
        const response = await this.request(`/progress/lesson/${lessonId}/status`);
        return response.json();
    } catch (error) {
        console.error('Error checking lesson progress:', error);
        return { success: false, completed: false };
    }
}

    async submitQuizAnswers(lessonId, answers) {
    const response = await this.request(`/quiz/lessons/${lessonId}/submit`, {
        method: 'POST',
        body: JSON.stringify({
            lessonId: lessonId,
            answers: answers
        })
    });
    return response.json();
}
async runCode(code, language, input = '') {
    console.log('🔵 runCode - отправляю запрос:', {
        code: code.substring(0, 50) + '...',
        language,
        inputLength: input.length
    });
    
    try {
        const response = await this.request('/code/execute', {
            method: 'POST',
            body: JSON.stringify({
                code: code,
                language: language, 
                lessonId: window.app?.courseManager?.currentLesson?.id || '',
                languageId: language === 'python' ? '11111111-1111-1111-1111-111111111111' : 
                            language === 'javascript' ? '22222222-2222-2222-2222-222222222222' : '',
                stdin: input
            })
        });
        
        const data = await response.json();
        console.log('🔵 runCode - ответ:', data);
        return data;
    } catch (error) {
        console.error('🔴 runCode - ошибка:', error);
        return {
            success: false,
            result: {
                output: '',
                error: error.message,
                executionTime: 0
            }
        };
    }
}
async markTheoryAsRead(lessonId) {
        try {
            const response = await this.request(`/progress/lesson/${lessonId}/mark-theory-read`, {
                method: 'POST'
            });
            return await response.json();
        } catch (error) {
            console.error('Error marking theory as read:', error);
            return { success: false, error: error.message };
        }
    }

    async getLessonDetailedStatus(lessonId) {
        try {
            const response = await this.request(`/progress/lesson/${lessonId}/detailed-status`);
            return await response.json();
        } catch (error) {
            console.error('Error getting lesson detailed status:', error);
            return { success: false, error: error.message };
        }
    }

    async checkLessonProgress(lessonId) {
        try {
            const response = await this.request(`/progress/lesson/${lessonId}/status`);
            return await response.json();
        } catch (error) {
            console.error('Error checking lesson progress:', error);
            return { success: false, completed: false };
        }
    }

async runCodeTests(lessonId, code, language, input = '') {
    try {
        console.log('Calling runCodeTests API:', { lessonId, language, inputLength: input.length });
        
        const response = await this.request('/code/lessons/' + lessonId + '/run-tests', {
            method: 'POST',
            body: JSON.stringify({
                code: code,
                language: language,
                languageId: language === 'python' ? '11111111-1111-1111-1111-111111111111' : '',
                stdin: input  
            })
        });
        
        const data = await response.json();
        console.log('runCodeTests response:', data);
        return data;
    } catch (error) {
        console.error('Error running code tests:', error);
        return { success: false, error: error.message };
    }
}
async getCodeTemplate(lessonId, languageId, userId = null) {
    const url = userId 
        ? `/courses/lessons/${lessonId}/code-template/${languageId}?userId=${userId}`
        : `/courses/lessons/${lessonId}/code-template/${languageId}`;
    
    const response = await this.request(url);
    return response.json();
}

async getUserStatistics() {
    const response = await this.request('/progress/user-statistics');
    return response.json();
}

async getTeacherDashboard() {
    const response = await this.request('/teacher/dashboard');
    return response.json();
}

async getCourse(courseId) {
    const response = await this.request(`/courses/${courseId}`);
    const data = await response.json();
    
    console.log('🔥🔥🔥 СЫРОЙ ОТВЕТ ОТ СЕРВЕРА:', JSON.stringify(data, null, 2));
    
    if (data.success && data.course) {
        console.log('🔥🔥🔥 ДАННЫЕ КУРСА ИЗ СЕРВЕРА:', JSON.stringify(data.course, null, 2));
        
        return {
            success: true,
            course: {
                id: data.course.id,
                title: data.course.title,
                description: data.course.description,
                difficultyLevel: data.course.difficultyLevel,
                isPublished: data.course.isPublished,
                createdBy: data.course.createdBy,
                programmingLanguageId: data.course.programmingLanguageId,
                programmingLanguageName: data.course.programmingLanguageName
            }
        };
    }
    return data;
}

async getStudentProgress(studentId, courseId) {
    const response = await this.request(`/teacher/students/${studentId}/courses/${courseId}/progress`);
    return response.json();
}
async performTeacherAction(action) {
    const response = await this.request('/teacher/action', {
        method: 'POST',
        body: JSON.stringify(action)
    });
    return response.json();
}
async getUserCertificates() {
    const response = await this.request('/achievements/certificates');
    return response.json();
}

async saveCertificate(certificateData) {
    const response = await this.request('/achievements/certificates', {
        method: 'POST',
        body: JSON.stringify(certificateData)
    });
    return response.json();
}

async getCertificate(certificateId) {
    const response = await this.request(`/achievements/certificates/${certificateId}`);
    return response.json();
}
async getUser(userId) {
    const response = await this.request(`/users/${userId}`);
    return response.json();
}

async getAllTeacherStudents() {
    try {
        const dashboard = await this.getTeacherDashboard();
        
        if (!dashboard.success || !dashboard.dashboard.courses) {
            return { success: true, students: [] };
        }
        
        const promises = dashboard.dashboard.courses.map(course => 
            this.getCourseStudents(course.id)
        );
        
        const results = await Promise.all(promises);
        
        const allStudents = [];
        const seenUsers = new Set();
        
        results.forEach((result, index) => {
            if (result.success && result.students) {
                const courseId = dashboard.dashboard.courses[index].id;
                const courseTitle = dashboard.dashboard.courses[index].title;
                
                result.students.forEach(student => {
                    const studentWithCourse = {
                        ...student,
                        courses: [{
                            courseId,
                            courseTitle,
                            progress: student.courseProgress
                        }]
                    };
                    
                    if (seenUsers.has(student.userId)) {
                        const existing = allStudents.find(s => s.userId === student.userId);
                        if (existing) {
                            existing.courses.push({
                                courseId,
                                courseTitle,
                                progress: student.courseProgress
                            });
                        }
                    } else {
                        seenUsers.add(student.userId);
                        allStudents.push(studentWithCourse);
                    }
                });
            }
        });
        
        return { success: true, students: allStudents };
    } catch (error) {
        console.error('Ошибка загрузки всех студентов:', error);
        return { success: false, error: error.message };
    }
}
async getProgrammingLanguages() {
    const response = await this.request('/languages');
    return response.json();
}

async getCourseStudents(courseId) {
    try {
        const response = await this.request(`/teacher/courses/${courseId}/students`);
        const data = await response.json();
        
        console.log('📊 getCourseStudents ответ:', data);
        
        return data;
    } catch (error) {
        console.error('Ошибка загрузки студентов курса:', error);
        return { success: false, error: error.message };
    }
}
}
