import { ApiService } from '../services/ApiService.js';
import { UIManager } from './UIManager.js';

export class AchievementsManager {
    constructor(apiService, uiManager) {
        this.api = apiService;
        this.uiManager = uiManager;
    }

    async initialize() {
        this.setupEventListeners();
    }

    setupEventListeners() {
        const btnAchievements = document.getElementById('view-achievements');
        if (btnAchievements) {
            btnAchievements.addEventListener('click', () => {
                this.showAchievements();
            });
        }
    }

    async showAchievements() {
        try {
            this.uiManager.showLoading(true);
            const result = await this.api.getUserCertificates();
            
            if (result.success) {
                this.renderAchievementsModal(result.certificates);
            } else {
                this.uiManager.showToast('Ошибка загрузки достижений', 'error');
            }
        } catch (error) {
            console.error('Ошибка загрузки достижений:', error);
            this.uiManager.showToast('Ошибка загрузки достижений', 'error');
        } finally {
            this.uiManager.showLoading(false);
        }
    }

    renderAchievementsModal(certificates) {
        let modal = document.getElementById('achievements-modal');
        
        if (!modal) {
            modal = document.createElement('div');
            modal.id = 'achievements-modal';
            modal.className = 'modal';
            document.body.appendChild(modal);
        }

        modal.innerHTML = `
            <div class="modal-card" style="max-width: 800px; width: 90%; max-height: 80vh; overflow-y: auto;">
                <header style="display: flex; justify-content: space-between; align-items: center; padding: 15px 20px; border-bottom: 1px solid #e2e8f0;">
                    <h3 style="margin: 0;">Мои достижения</h3>
                    <button class="close-btn" onclick="document.getElementById('achievements-modal').classList.add('hidden')">✕</button>
                </header>
                
                <div class="modal-content" style="padding: 20px;">
                    ${this.renderCertificatesList(certificates)}
                </div>
            </div>
        `;

        modal.classList.remove('hidden');
    }

    renderCertificatesList(certificates) {
        if (!certificates || certificates.length === 0) {
            return `
                <div class="empty-state" style="text-align: center; padding: 40px;">
                    <div style="font-size: 64px; margin-bottom: 20px;">🏆</div>
                    <h3>У вас пока нет сертификатов</h3>
                    <p class="muted">Завершите курс, чтобы получить свой первый сертификат!</p>
                </div>
            `;
        }

        return `
            <div style="display: grid; grid-template-columns: repeat(auto-fill, minmax(250px, 1fr)); gap: 20px;">
                ${certificates.map(cert => `
                    <div class="certificate-card" style="background: linear-gradient(135deg, #f8f9fa 0%, #ffffff 100%); border: 1px solid #e2e8f0; border-radius: 10px; padding: 20px; text-align: center; box-shadow: 0 2px 5px rgba(0,0,0,0.05);">
                        <div style="font-size: 48px; margin-bottom: 15px;">🎓</div>
                        <h4 style="margin: 10px 0; color: #2d3748;">${cert.courseName}</h4>
                        <p style="color: #718096; font-size: 14px; margin: 5px 0;">
                            ${new Date(cert.issuedAt).toLocaleDateString('ru-RU', { year: 'numeric', month: 'long', day: 'numeric' })}
                        </p>
                        <p style="font-family: monospace; color: #a0aec0; font-size: 12px; margin: 5px 0 15px 0;">
                            № ${cert.certificateNumber}
                        </p>
                        <button class="btn-primary btn-sm" onclick="app.achievementsManager.viewCertificate('${cert.id}', '${cert.certificateNumber}', '${cert.studentName}', '${cert.courseName}', '${cert.issuedAt}')">
                            Просмотреть
                        </button>
                    </div>
                `).join('')}
            </div>
        `;
    }

    viewCertificate(certId, certNumber, studentName, courseName, issuedAt, teacherName = '') {
    const date = new Date(issuedAt);
    const dateStr = date.toISOString().split('T')[0];
    
    window.open(
        `certificate.html?name=${encodeURIComponent(studentName)}&course=${encodeURIComponent(courseName)}&date=${dateStr}&cert=${certNumber}&teacher=${encodeURIComponent(teacherName)}`,
        '_blank'
    );
}

    async saveCertificateForCompletedCourse(userId, courseId, studentName, courseName, teacherName = '') {
    // Генерируем уникальный номер
    const date = new Date();
    const dateStr = date.toISOString().slice(0,10).replace(/-/g, '');
    const random = Math.random().toString(36).substring(2, 10).toUpperCase();
    const certNumber = `LB-${dateStr}-${random}`;

    console.log('📝 Отправляем запрос на создание сертификата:', {
        courseId, certNumber, studentName, courseName, teacherName
    });

    try {
        const result = await this.api.saveCertificate({
            courseId: courseId,
            certificateNumber: certNumber,
            studentName: studentName,
            courseName: courseName,
            teacherName: teacherName
        });

        console.log('📦 Ответ от сервера:', result);

        if (result.success && result.certificate) {
            this.uiManager.showToast('🎉 Новый сертификат добавлен в достижения!', 'success');
            return result.certificate;
        } 
        else if (result.success && result.message === 'Сертификат уже существует') {
            console.log('📌 Сертификат уже существует, получаем последний');
            const certs = await this.api.getUserCertificates();
            if (certs.success && certs.certificates.length > 0) {
                return certs.certificates[0];
            }
        }
        
        console.error('❌ Ошибка сохранения сертификата:', result.error);
        return null;
        
    } catch (error) {
        console.error('❌ Ошибка:', error);
        return null;
    }
}
}