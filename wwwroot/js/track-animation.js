/**
 * Track Animation Module - Smooth car movement with 60fps using requestAnimationFrame
 * Handles car position interpolation, glow effects, and special states (pit, speed trap)
 */

let carElement = null;
let targetX = 600;
let targetY = 700;
let currentX = 600;
let currentY = 700;
let isAnimating = false;
let animationFrameId = null;

/**
 * Initialize or update car position on track
 * @param {number} x - Target X coordinate
 * @param {number} y - Target Y coordinate
 * @param {number} speed - Current speed in km/h
 * @param {boolean} isPit - Whether car is in pit lane
 * @param {boolean} isST - Whether car is at speed trap
 */
window.animateCar = function(x, y, speed, isPit, isST) {
    targetX = x;
    targetY = y;

    // Create car element if it doesn't exist
    if (!carElement) {
        initializeCarElement();
    }

    // Update car appearance based on state
    updateCarAppearance(speed, isPit, isST);

    // Start animation loop if not already running
    if (!isAnimating) {
        isAnimating = true;
        animate();
    }
};

/**
 * Create the car DOM element
 */
function initializeCarElement() {
    const svgElement = document.querySelector('svg') || document.querySelector('.track-svg');

    if (!svgElement) {
        console.error('Track SVG not found');
        return;
    }

    // Create car as div overlay
    carElement = document.createElement('div');
    carElement.id = 'car-dot';
    carElement.className = 'car-dot';

    // Position absolutely over SVG
    const container = svgElement.parentElement;
    if (container) {
        container.style.position = 'relative';
        container.appendChild(carElement);
    }
}

/**
 * Update car visual appearance based on current state
 */
function updateCarAppearance(speed, isPit, isST) {
    if (!carElement) return;

    // Speed trap flash (purple/magenta)
    if (isST && speed > 200) {
        carElement.style.background = 'linear-gradient(135deg, #ff00ff, #ff0066)';
        carElement.style.boxShadow = '0 0 30px #ff00ff, 0 0 50px rgba(255, 0, 255, 0.5)';
        carElement.style.transform = 'scale(1.3)';

        // Reset after 300ms
        setTimeout(() => {
            if (carElement && !isPit) {
                resetCarAppearance();
            }
        }, 300);
    }
    // Pit lane (yellow)
    else if (isPit) {
        carElement.style.background = 'linear-gradient(135deg, #ffff00, #ffd700)';
        carElement.style.boxShadow = '0 0 25px #ffff00, 0 0 40px rgba(255, 255, 0, 0.5)';
        carElement.style.transform = 'scale(1.1)';
    }
    // Normal racing (green/cyan)
    else {
        resetCarAppearance();
    }

    // Add speed-based scaling
    const speedScale = 1 + (speed / 1000);
    const currentTransform = carElement.style.transform || 'scale(1)';
    if (!currentTransform.includes('scale')) {
        carElement.style.transform = `scale(${speedScale})`;
    }
}

/**
 * Reset car to default appearance
 */
function resetCarAppearance() {
    if (!carElement) return;

    carElement.style.background = 'linear-gradient(135deg, #00ff88, #00d4ff)';
    carElement.style.boxShadow = '0 0 20px #00ff88, 0 0 40px rgba(0, 255, 136, 0.5)';
    carElement.style.transform = 'scale(1)';
}

/**
 * Main animation loop using requestAnimationFrame
 */
function animate() {
    if (!carElement) {
        isAnimating = false;
        return;
    }

    // Calculate distance to target
    const dx = targetX - currentX;
    const dy = targetY - currentY;
    const distance = Math.sqrt(dx * dx + dy * dy);

    // Smooth interpolation (lerp with factor 0.2)
    if (distance > 0.5) {
        currentX += dx * 0.2;
        currentY += dy * 0.2;

        // Update car position
        carElement.style.left = `${currentX - 8}px`;
        carElement.style.top = `${currentY - 8}px`;

        // Continue animation
        animationFrameId = requestAnimationFrame(animate);
    } else {
        // Target reached
        isAnimating = false;
        if (animationFrameId) {
            cancelAnimationFrame(animationFrameId);
            animationFrameId = null;
        }
    }
}

/**
 * Add car trail effect (optional enhancement)
 */
window.addCarTrail = function(x, y) {
    const trail = document.createElement('div');
    trail.className = 'car-trail';
    trail.style.left = `${x - 4}px`;
    trail.style.top = `${y - 4}px`;

    const container = carElement?.parentElement;
    if (container) {
        container.appendChild(trail);

        // Remove after fade out
        setTimeout(() => trail.remove(), 1000);
    }
};

/**
 * Cleanup function
 */
window.disposeCarAnimation = function() {
    if (animationFrameId) {
        cancelAnimationFrame(animationFrameId);
        animationFrameId = null;
    }

    if (carElement) {
        carElement.remove();
        carElement = null;
    }

    isAnimating = false;
};

/**
 * Flash sector crossing
 */
window.flashSectorCrossing = function(sector) {
    const sectorElement = document.getElementById(`sector-${sector}`);
    if (sectorElement) {
        sectorElement.classList.add('sector-flash');
        setTimeout(() => {
            sectorElement.classList.remove('sector-flash');
        }, 500);
    }
};

// Log initialization
console.log('Track animation module loaded');
