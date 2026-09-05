"""Measured-time video and every-frame review sheets; never alters game assets or movement."""
from __future__ import annotations
import argparse
import csv
import json
import math
import shutil
import subprocess
from collections import defaultdict
from pathlib import Path
from statistics import median
from PIL import Image, ImageDraw, ImageFont

IDS = ['player', 'older_sister', 'father', 'mother']
COLORS = [(70, 225, 250), (90, 225, 140), (255, 177, 72), (255, 124, 181)]

def number(row, key):
    return float(row[key])

def point(row, prefix):
    return (number(row, prefix+'X'), number(row, prefix+'Y'))

def stats(values):
    return {'median': round(median(values), 6), 'max': round(max(values), 6), 'min': round(min(values), 6)} if values else None

def run(artifact, stats_only=False):
    rows = list(csv.DictReader((artifact/'walk-trace.csv').open(encoding='utf-8-sig')))
    proj = next(csv.DictReader((artifact/'projection.csv').open()))
    origin = (float(proj['originX']),float(proj['originY']))
    bx = (float(proj['basisXX']),float(proj['basisXY']))
    by = (float(proj['basisYX']),float(proj['basisYY']))
    area = abs(bx[0]*by[1] - bx[1]*by[0])
    def lane_error(row):
        # Perpendicular screen distance to the nearest cardinal cell-centre line.
        # This does not prove that the selected route/turn is correct.
        gx, gy = number(row,'gridX'), number(row,'gridY')
        return min(abs(gx-round(gx))*area/math.hypot(*by),
                   abs(gy-round(gy))*area/math.hypot(*bx))
    by_actor, by_frame = defaultdict(list), defaultdict(dict)
    for row in rows:
        by_actor[row['member']].append(row)
        by_frame[int(row['frame'])][row['member']] = row
    summary = {'artifact': str(artifact), 'capture': (artifact/'audit-capture.txt').read_text(),
               'footGate': 'moving foot midpoint projected to ground: median <= 4px, max <= 8px',
               'note': ('Ankle contact travel includes foot roll; it is not an independent skin-slip PASS/FAIL. '
                        'Retained path polyline distance may increase when a replan omits the segment from '
                        'the current root to its first waypoint; do not call it tile-lane deviation. '
                        'Lowest skin Y is sampled every six frames and is not a completed grounding gate.'), 'actors': {}}
    for member, items in by_actor.items():
        moving = [r for r in items if number(r,'displacement') > 0.0001]
        straight = []
        turns = 0
        last_turn = -999
        leads = []
        contact_steps = []
        for i, row in enumerate(items):
            if not i: continue
            prev = items[i-1]
            yaw_delta = abs((number(row,'yaw') - number(prev,'yaw') + 180) % 360 - 180)
            if yaw_delta > 1:
                if number(row, 'seconds') - last_turn > .25: turns += 1
                last_turn = number(row,'seconds')
            if number(row,'displacement') > .0001 and number(row,'seconds') - last_turn > .25:
                straight.append(row)
            if number(row,'displacement') > .0001:
                lead = number(row, 'footLead')
                if abs(lead) > .02: leads.append(1 if lead > 0 else -1)
                for side in ['left','right']:
                    if row[side+'Contact'] == '1' and prev[side+'Contact'] == '1':
                        contact_steps.append(math.hypot(number(row,side+'WorldX')-number(prev,side+'WorldX'),
                                                       number(row,side+'WorldZ')-number(prev,side+'WorldZ')))
        mid = stats([number(r,'footMidErrorPx') for r in moving])
        straight_mid = stats([number(r,'footMidErrorPx') for r in straight])
        paths = [number(r,'pathErrorPx') for r in moving if number(r,'pathErrorPx') >= 0]
        summary['actors'][member] = {'frames': len(items), 'movingFrames':len(moving),
            'seconds': round(number(items[-1],'seconds'),3), 'footMidPx': mid, 'straightFootMidPx':straight_mid,
            'footMidGate': bool(mid and mid['median'] <= 4 and mid['max'] <= 8), 'retainedPathPolylinePx':stats(paths),
            'nearestCardinalCellCentreLinePx':stats([lane_error(r) for r in moving]),
            'lowestSkinY':stats([number(r,'lowestMeshY') for r in moving]),
            'distance':round(number(items[-1],'gaitDistance')-number(items[0],'gaitDistance'),4),
            'leadAlternations':sum(a!=b for a,b in zip(leads,leads[1:])), 'turnEpisodes':turns,
            'contactAnkleDeltaWorld':stats(contact_steps), 'directions':sorted(set(r['direction'] for r in moving))}
    review = artifact/'review'
    review.mkdir(exist_ok=True)
    (review/'analysis.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(summary,ensure_ascii=False,indent=2),flush=True)
    if stats_only: return
    def cell(x,y): return (origin[0]+x*bx[0]+y*by[0],origin[1]+x*bx[1]+y*by[1])
    font = ImageFont.truetype('C:/Windows/Fonts/consola.ttf',12)
    tiny = ImageFont.truetype('C:/Windows/Fonts/consola.ttf',10)
    for name in ['marked','closeups','sheets']: (review/name).mkdir(exist_ok=True)
    sheet = None
    frame_list = sorted(by_frame)
    durations = []
    for j, frame in enumerate(frame_list):
        current = by_frame[frame]
        time = number(current['player'],'seconds')
        next_time = number(by_frame[frame_list[j+1]]['player'],'seconds') if j+1<len(frame_list) else time+.1
        durations.append(max(.01,next_time-time))
        frame_path = artifact/'frames'/f'frame-{frame:04}.png'
        if not frame_path.exists(): frame_path = frame_path.with_suffix('.tga')
        im = Image.open(frame_path).convert('RGB')
        draw = ImageDraw.Draw(im)
        draw.rectangle((0,0,1280,38),fill=(16,30,34))
        draw.text((10,4),f'{artifact.name} | real-time {time:.2f}s | frame {frame}',font=font,fill='white')
        draw.text((10,20),'YELLOW = real cell boundary | CYAN + = movement root | PINK + = feet midpoint projected to floor',font=font,fill='white')
        for member in IDS:
            r=current[member]; x,y=number(r,'cellX'),number(r,'cellY')
            polygon=[cell(x-.5,y-.5),cell(x+.5,y-.5),cell(x+.5,y+.5),cell(x-.5,y+.5)]
            draw.line(polygon+[polygon[0]],fill=(255,230,75),width=1)
            for prefix,color in [('root',(0,245,255)),('footMid',(255,80,215))]:
                px,py=point(r,prefix)
                draw.line((px-4,py,px+4,py),fill=color,width=1)
                draw.line((px,py-4,px,py+4),fill=color,width=1)
        im.save(review/'marked'/f'frame-{frame:04}.png')
        quad=Image.new('RGB',(224,296),(20,30,34)); qdraw=ImageDraw.Draw(quad)
        qdraw.text((4,0),f'frame {frame:04}   {time:6.2f}s',fill='white',font=font)
        for idx,member in enumerate(IDS):
            r=current[member]; px,py=point(r,'root'); cx=round(px); cy=round(py)
            crop=im.crop((cx-56,cy-115,cx+56,cy+21))
            dst=(idx%2*112,20+idx//2*138)
            quad.paste(crop,dst)
            qdraw.text((dst[0]+2,dst[1]+1),member,fill=COLORS[idx],font=tiny,stroke_width=1,stroke_fill=(0,0,0))
        quad.resize((672,888),Image.Resampling.NEAREST).save(review/'closeups'/f'frame-{frame:04}.png')
        if j%20==0: sheet=Image.new('RGB',(1120,1184),(20,30,34))
        sheet.paste(quad,(j%5*224,(j%20)//5*296))
        if j%20==19 or j==len(frame_list)-1: sheet.save(review/'sheets'/f'all-frames-{j//20:02}.png')
    ffmpeg=shutil.which('ffmpeg')
    for folder,outname in [('marked','tile-centres-overview.mp4'),('closeups','four-actors-closeup.mp4')]:
        concat=review/f'{folder}-times.txt'
        lines=[]
        for frame,duration in zip(frame_list,durations):
            path=(review/folder/f'frame-{frame:04}.png').resolve().as_posix()
            lines.extend([f"file '{path}'",f'duration {duration:.6f}'])
        lines.append(lines[-2])
        concat.write_text('\n'.join(lines)+'\n',encoding='utf-8')
        subprocess.run([ffmpeg,'-hide_banner','-loglevel','error','-y','-f','concat','-safe','0','-i',str(concat),
                        '-vf','fps=30','-c:v','libx264','-crf','18','-pix_fmt','yuv420p','-an','-movflags','+faststart',
                        str(review/outname)],check=True)
    (review/'README.md').write_text(
        '# Continuous normal opening walk audit\n\n'+
        'Every actual rendered frame is retained; video timing uses measured capture timestamps, not a sped-up walk.\n'+
        'Yellow: the real stationary tile cell. Cyan cross: semantic movement root. Pink cross: ground-projected ankle midpoint.\n'+
        'Path centring and visible-foot centring are separate tests. The ankle midpoint is not a pixel-centroid measurement.\n'+
        'All frames are also arranged in sheets in chronological order. No route/pose/clock injection, no production edit.\n'+
        'analysis.json contains the measurements; CAPTURED alone does not mean the walk is approved.\n',encoding='utf-8')

if __name__=='__main__':
    parser=argparse.ArgumentParser(); parser.add_argument('artifact',type=Path)
    parser.add_argument('--stats-only',action='store_true',help='Reuse captured frames and existing videos; update measurements only.')
    args=parser.parse_args()
    run(args.artifact.resolve(), args.stats_only)
