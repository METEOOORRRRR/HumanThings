from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
QA=Path(r'D:\HumanThings\QA\ToxicBunnyTexture')
font=ImageFont.truetype(r'C:\Windows\Fonts\malgun.ttf',32)
small=ImageFont.truetype(r'C:\Windows\Fonts\malgun.ttf',19)
def comparison(view,filename):
    a=Image.open(QA/'original_final_lighting'/f'{view}.png').convert('RGB')
    b=Image.open(QA/'final'/f'{view}.png').convert('RGB')
    w,h=a.size
    page=Image.new('RGB',(w*2,h+108),(30,33,38));d=ImageDraw.Draw(page)
    d.text((30,15),'원본',font=font,fill='white')
    d.text((w+30,15),'수정본 · 실제 3D 렌더',font=font,fill='white')
    page.paste(a,(0,70));page.paste(b,(w,70))
    d.text((24,h+77),'동일한 모델 형상 · 동일한 카메라와 조명 · 텍스처 및 재질만 변경',font=small,fill=(198,204,211))
    page.save(QA/'final'/filename,quality=93)
comparison('face','Face_Before_After.jpg')
comparison('front','Body_Before_After.jpg')
views=['front','right','back','left','threequarter']
page=Image.new('RGB',(2100,610),(30,33,38));d=ImageDraw.Draw(page)
for i,v in enumerate(views):
    im=Image.open(QA/'final'/f'{v}.png').convert('RGB');im=im.resize((420,540),Image.Resampling.LANCZOS)
    page.paste(im,(i*420,50));d.text((i*420+20,9),v.upper(),font=small,fill='white')
page.save(QA/'final'/'Turnaround_3D.jpg',quality=93)
print('Review comparisons created from actual GLB renders.')
