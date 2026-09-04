import os
from PIL import Image, ImageDraw

def create_icon(filename, size, is_category=False):
    width, height = size
    img = Image.new("RGBA", size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    
    margin = int(width * 0.08)
    r = int(width * 0.20)
    bg_color = (0, 0, 0, 0)
    border_color = (255, 255, 255, 255) # Pure White
    
    if bg_color[3] > 0:
        draw.rounded_rectangle(
            [(margin, margin), (width - margin, height - margin)],
            radius=r,
            fill=bg_color,
            outline=border_color,
            width=max(1, int(width * 0.05))
        )
        
    cx, cy = width / 2, height / 2
    cr = int(width * 0.22)
    
    # Draw Clock Circle
    draw.ellipse(
        [(cx - cr, cy - cr), (cx + cr, cy + cr)],
        outline=(255, 255, 255, 255),
        width=max(1, int(width * 0.04))
    )
    
    # Hour hand (pointing to 2 o'clock)
    hr_len = cr * 0.6
    draw.line(
        [(cx, cy), (cx + hr_len * 0.5, cy - hr_len * 0.866)],
        fill=(255, 255, 255, 255),
        width=max(1, int(width * 0.06))
    )
    
    # Minute hand (pointing to 12 o'clock)
    min_len = cr * 0.8
    draw.line(
        [(cx, cy), (cx, cy - min_len)],
        fill=(255, 255, 255, 255),
        width=max(1, int(width * 0.04))
    )
    
    # Center Pin
    pin_r = max(1, int(width * 0.06))
    draw.ellipse(
        [(cx - pin_r, cy - pin_r), (cx + pin_r, cy + pin_r)],
        fill=(255, 255, 255, 255)
    )
    
    img.save(filename, "PNG")

def main():
    os.makedirs("imgs", exist_ok=True)
    create_icon("imgs/plugin-icon.png", (48, 48))
    create_icon("imgs/plugin-icon@2x.png", (96, 96))
    create_icon("imgs/category-icon.png", (28, 28), is_category=True)
    create_icon("imgs/category-icon@2x.png", (56, 56), is_category=True)
    create_icon("imgs/action-icon.png", (72, 72))
    create_icon("imgs/action-icon@2x.png", (144, 144))
    print("All Stream Deck icons generated in /imgs folder!")

if __name__ == "__main__":
    main()
