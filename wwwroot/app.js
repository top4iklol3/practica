const API_BASE = '/api/FileStorage';
let currentPath = '';

// Инициализация
document.addEventListener('DOMContentLoaded', () => {
    loadFiles();
    
    // Обработчики событий
    document.getElementById('fileInput').addEventListener('change', handleFileUpload);
    document.getElementById('createFolderBtn').addEventListener('click', showCreateFolderModal);
    document.getElementById('createFolderConfirm').addEventListener('click', createFolder);
    document.getElementById('cancelFolder').addEventListener('click', hideCreateFolderModal);
    document.getElementById('refreshBtn').addEventListener('click', loadFiles);
    document.getElementById('backBtn').addEventListener('click', goBack);
    
    // Закрытие модального окна
    document.querySelector('.close').addEventListener('click', hideCreateFolderModal);
    window.addEventListener('click', (e) => {
        const modal = document.getElementById('folderModal');
        if (e.target === modal) {
            hideCreateFolderModal();
        }
    });
});

// Загрузка списка файлов
async function loadFiles() {
    const loading = document.getElementById('loading');
    const fileList = document.getElementById('fileList');
    const error = document.getElementById('error');
    
    loading.style.display = 'block';
    fileList.innerHTML = '';
    error.style.display = 'none';
    
    try {
        const response = await fetch(`${API_BASE}/list?path=${encodeURIComponent(currentPath)}`);
        const data = await response.json();
        
        if (!response.ok) {
            throw new Error(data.error || 'Ошибка загрузки');
        }
        
        updateBreadcrumb(data.currentPath);
        displayFiles(data.items);
        updateBackButton();
    } catch (err) {
        error.textContent = `Ошибка: ${err.message}`;
        error.style.display = 'block';
    } finally {
        loading.style.display = 'none';
    }
}

// Отображение файлов и папок
function displayFiles(items) {
    const fileList = document.getElementById('fileList');
    
    if (items.length === 0) {
        fileList.innerHTML = `
            <div class="empty-state">
                <div class="empty-state-icon">📂</div>
                <p>Папка пуста</p>
            </div>
        `;
        return;
    }
    
    fileList.innerHTML = items.map((item, index) => {
        const delay = index * 50;
        if (item.type === 'folder') {
            return `
                <div class="file-item folder" onclick="openFolder('${escapePath(item.path)}')" style="animation: fadeInUp 0.5s ease-out ${delay}ms both;">
                    <div class="file-icon">📁</div>
                    <div class="file-info">
                        <div class="file-name">${escapeHtml(item.name)}</div>
                        <div class="file-meta">
                            <span>📂 Папка</span>
                            <span>📅 ${formatDate(item.created)}</span>
                        </div>
                    </div>
                    <div class="file-actions">
                        <button class="btn btn-danger" onclick="event.stopPropagation(); deleteItem('${escapePath(item.path)}')">🗑️ Удалить</button>
                    </div>
                </div>
            `;
        } else {
            const size = formatFileSize(item.size);
            return `
                <div class="file-item" onclick="downloadFile('${escapePath(item.path)}')" style="animation: fadeInUp 0.5s ease-out ${delay}ms both;">
                    <div class="file-icon">${getFileIcon(item.name)}</div>
                    <div class="file-info">
                        <div class="file-name">${escapeHtml(item.name)}</div>
                        <div class="file-meta">
                            <span>💾 ${size}</span>
                            <span>📅 ${formatDate(item.modified)}</span>
                        </div>
                    </div>
                    <div class="file-actions">
                        <button class="btn btn-primary" onclick="event.stopPropagation(); downloadFile('${escapePath(item.path)}')">⬇️ Скачать</button>
                        <button class="btn btn-danger" onclick="event.stopPropagation(); deleteItem('${escapePath(item.path)}')">🗑️ Удалить</button>
                    </div>
                </div>
            `;
        }
    }).join('');
}

// Открыть папку
function openFolder(path) {
    currentPath = path;
    loadFiles();
}

// Загрузка файлов
async function handleFileUpload(event) {
    const files = event.target.files;
    if (files.length === 0) return;
    
    const formData = new FormData();
    for (let file of files) {
        formData.append('files', file);
    }
    
    const loading = document.getElementById('loading');
    const error = document.getElementById('error');
    
    loading.style.display = 'block';
    error.style.display = 'none';
    
    try {
        const response = await fetch(`${API_BASE}/upload?path=${encodeURIComponent(currentPath)}`, {
            method: 'POST',
            body: formData
        });
        
        const data = await response.json();
        
        if (!response.ok) {
            throw new Error(data.error || 'Ошибка загрузки');
        }
        
        loadFiles();
    } catch (err) {
        error.textContent = `Ошибка: ${err.message}`;
        error.style.display = 'block';
    } finally {
        loading.style.display = 'none';
        event.target.value = ''; // Сброс input
    }
}

// Скачать файл
function downloadFile(path) {
    window.open(`${API_BASE}/download?path=${encodeURIComponent(path)}`, '_blank');
}

// Создать папку
function showCreateFolderModal() {
    document.getElementById('folderModal').style.display = 'block';
    document.getElementById('folderNameInput').value = 'Новая папка';
    document.getElementById('folderNameInput').focus();
    document.getElementById('folderNameInput').select();
}

function hideCreateFolderModal() {
    document.getElementById('folderModal').style.display = 'none';
}

async function createFolder() {
    const folderName = document.getElementById('folderNameInput').value.trim();
    if (!folderName) {
        alert('Введите название папки');
        return;
    }
    
    const loading = document.getElementById('loading');
    const error = document.getElementById('error');
    
    loading.style.display = 'block';
    error.style.display = 'none';
    
    try {
        const response = await fetch(`${API_BASE}/folder?path=${encodeURIComponent(currentPath)}`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ name: folderName })
        });
        
        const data = await response.json();
        
        if (!response.ok) {
            throw new Error(data.error || 'Ошибка создания папки');
        }
        
        hideCreateFolderModal();
        loadFiles();
    } catch (err) {
        error.textContent = `Ошибка: ${err.message}`;
        error.style.display = 'block';
    } finally {
        loading.style.display = 'none';
    }
}

// Удалить файл или папку
async function deleteItem(path) {
    if (!confirm('Вы уверены, что хотите удалить этот элемент?')) {
        return;
    }
    
    const loading = document.getElementById('loading');
    const error = document.getElementById('error');
    
    loading.style.display = 'block';
    error.style.display = 'none';
    
    try {
        const response = await fetch(`${API_BASE}/item?path=${encodeURIComponent(path)}`, {
            method: 'DELETE'
        });
        
        const data = await response.json();
        
        if (!response.ok) {
            throw new Error(data.error || 'Ошибка удаления');
        }
        
        loadFiles();
    } catch (err) {
        error.textContent = `Ошибка: ${err.message}`;
        error.style.display = 'block';
    } finally {
        loading.style.display = 'none';
    }
}

// Обновить breadcrumb
function updateBreadcrumb(path) {
    const breadcrumb = document.querySelector('.breadcrumb');
    const breadcrumbPath = document.getElementById('breadcrumbPath');
    
    if (!path) {
        breadcrumbPath.innerHTML = '';
        return;
    }
    
    const parts = path.split('/').filter(p => p);
    let html = '';
    let current = '';
    
    parts.forEach((part, index) => {
        current += (current ? '/' : '') + part;
        html += `<span class="breadcrumb-separator">/</span>`;
        html += `<button class="breadcrumb-item" onclick="openFolder('${escapePath(current)}')">${escapeHtml(part)}</button>`;
    });
    
    breadcrumbPath.innerHTML = html;
}

// Обновить кнопку "Назад"
function updateBackButton() {
    const backBtn = document.getElementById('backBtn');
    if (currentPath) {
        backBtn.style.display = 'inline-block';
    } else {
        backBtn.style.display = 'none';
    }
}

// Вернуться назад
function goBack() {
    if (!currentPath) return;
    
    const parts = currentPath.split('/').filter(p => p);
    if (parts.length > 0) {
        parts.pop();
        currentPath = parts.join('/');
    } else {
        currentPath = '';
    }
    
    loadFiles();
}

// Вспомогательные функции
function formatFileSize(bytes) {
    if (bytes === 0) return '0 Б';
    const k = 1024;
    const sizes = ['Б', 'КБ', 'МБ', 'ГБ'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return Math.round(bytes / Math.pow(k, i) * 100) / 100 + ' ' + sizes[i];
}

function formatDate(dateString) {
    const date = new Date(dateString);
    return date.toLocaleString('ru-RU', {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit'
    });
}

function getFileIcon(fileName) {
    const ext = fileName.split('.').pop().toLowerCase();
    const icons = {
        'pdf': '📕',
        'txt': '📝',
        'doc': '📘', 'docx': '📘',
        'xls': '📊', 'xlsx': '📊',
        'ppt': '📽️', 'pptx': '📽️',
        'jpg': '🖼️', 'jpeg': '🖼️', 'png': '🖼️', 'gif': '🖼️', 'svg': '🎨', 'webp': '🖼️',
        'zip': '📦', 'rar': '📦', '7z': '📦', 'tar': '📦', 'gz': '📦',
        'mp3': '🎵', 'mp4': '🎬', 'avi': '🎬', 'mov': '🎬', 'wav': '🎵',
        'json': '📋', 'xml': '📋', 'csv': '📊',
        'html': '🌐', 'css': '🎨', 'js': '⚡', 'ts': '⚡',
        'exe': '⚙️', 'msi': '⚙️', 'dmg': '💿',
        'py': '🐍', 'java': '☕', 'cpp': '⚙️', 'c': '⚙️'
    };
    return icons[ext] || '📄';
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

function escapePath(path) {
    return path.replace(/'/g, "\\'");
}

