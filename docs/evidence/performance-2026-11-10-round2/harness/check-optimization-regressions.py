"""Replay failed reference suites with frozen engine DLLs and unchanged test code.

Byte equality here detects newly introduced differences. It does not waive any
original-Skia quality gate recorded by the separate validation sweep.
"""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--validation',type=Path,required=True)
parser.add_argument('--before-engine',type=Path,required=True)
parser.add_argument('--output',type=Path,required=True)
parser.add_argument('--include-passing-shaders',action='store_true')
args=parser.parse_args()
for name in ['SimdSkia.Core.dll','SimdSkia.Cpu.dll','SimdSkia.Compatibility.dll','SimdSkia.Imaging.dll']:
    if not (args.before_engine/name).is_file():raise SystemExit('Incomplete frozen engine: '+name)
args.output.mkdir(parents=True,exist_ok=False)
projects={
    'alpha-compatibility':'AlphaCompatibility','blends':'BlendOperations','canvas-operations':'CanvasOperations',
    'canvas-queries':'CanvasQueries','image-filters':'ImageFilters','image-operations':'ImageOperations',
    'mask-filters':'MaskFilters','path-boolean':'PathBoolean','path-effects':'PathEffects','pictures':'Pictures',
    'pixel-compatibility':'PixelCompatibility','raster-shaders':'RasterShaders','stroke-compatibility':'StrokeCompatibility',
    'text-blobs':'TextBlobs','final-raster':'FinalRaster','final-paint':'FinalPaint','gl-interop':'GlInterop',
    'skp-documents':'SkpDocuments',
}
shader_projects={'mesh-atlas':'MeshAtlas','runtime-effects':'RuntimeEffects','float-shaders':'FloatShaders','shader-compatibility':'ShaderCompatibility'}
if args.include_passing_shaders:projects.update(shader_projects)
statuses=json.loads((args.validation/'results.json').read_text(encoding='utf-8'))
rows=[]
for name,project in projects.items():
    suite='check-'+name
    if not any(row['name']==suite and (row['status']=='reference-gate-failed' or args.include_passing_shaders and name in shader_projects and row['status']=='passed') for row in statuses):
        continue
    source=Path('tests')/project/'Managed/bin/Release/net10.0'
    frozen=args.output/'workers'/project
    shutil.copytree(source,frozen)
    engine={}
    for dll in args.before_engine.glob('SimdSkia*.dll'):
        shutil.copy2(dll,frozen/dll.name)
        engine[dll.name]=hashlib.sha256(dll.read_bytes()).hexdigest()
    config=next(frozen.glob('*.runtimeconfig.json'))
    program=config.with_name(config.name.replace('.runtimeconfig.json','.dll'))
    dest=args.output/project
    command=['dotnet',str(program),str(dest)]
    if project=='SkpDocuments':command.append(str(args.validation/suite/'Seed'))
    with (args.output/(project+'.log')).open('wb') as log:
        subprocess.run(command,stdout=log,stderr=subprocess.STDOUT,check=True,timeout=240)
    current=args.validation/suite/'managed'
    compared=[];differences=[];derived=[]
    for path in current.rglob('*'):
        if not path.is_file() or path.name in ('local.json','managed-checks.json'):
            continue
        previous=dest/path.relative_to(current)
        if project=='ShaderCompatibility' and path.suffix=='.png' and path.with_suffix('.bgra').is_file():
            # The Python oracle creates these PNGs after the C# worker exits.
            # Replay compares their complete, uncompressed BGRA source instead.
            old_pixels=previous.with_suffix('.bgra')
            if not old_pixels.is_file() or path.with_suffix('.bgra').read_bytes()!=old_pixels.read_bytes():
                differences.append(str(path.relative_to(current)))
            derived.append(str(path.relative_to(current)))
            continue
        if not previous.is_file():
            differences.append(str(path.relative_to(current)))
            continue
        same=json.loads(path.read_text(encoding='utf-8-sig'))==json.loads(previous.read_text(encoding='utf-8-sig')) if path.suffix=='.json' else path.read_bytes()==previous.read_bytes()
        compared.append(str(path.relative_to(current)))
        if not same:differences.append(str(path.relative_to(current)))
    row=dict(suite=suite,project=project,command=command,files=compared,derivedPngComparedThroughBgra=derived,differences=differences,engineSha256=engine,unchanged=bool(compared) and not differences)
    rows.append(row)
    (args.output/'summary.json').write_text(json.dumps(rows,indent=2)+'\n',encoding='utf-8')
    print(suite,'unchanged' if row['unchanged'] else 'DIFFERENT',len(compared),'files',flush=True)
raise SystemExit(0 if all(row['unchanged'] for row in rows) else 2)
