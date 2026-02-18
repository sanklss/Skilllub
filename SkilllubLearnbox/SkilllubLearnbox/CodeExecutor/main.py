from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
import subprocess
import time

app = FastAPI(title="Code Executor")

class CodeRequest(BaseModel):
    code: str
    language: str
    stdin: str = ""
    timeout: int = 5

class CodeResponse(BaseModel):
    output: str
    error: str
    execution_time: float
    success: bool

@app.post("/execute")
async def execute_code(request: CodeRequest):
    start = time.time()
    
    try:
        if request.language == "python":
            result = subprocess.run(
                ["python", "-c", request.code],
                input=request.stdin, 
                capture_output=True,
                text=True,
                timeout=request.timeout
            )
            
            return CodeResponse(
                output=result.stdout,
                error=result.stderr,
                execution_time=(time.time() - start) * 1000,
                success=result.returncode == 0
            )
            
        elif request.language == "javascript" or request.language == "js":
            result = subprocess.run(
                ["node", "-e", request.code],
                input=request.stdin,
                capture_output=True,
                text=True,
                timeout=request.timeout
            )
            
            return CodeResponse(
                output=result.stdout,
                error=result.stderr,
                execution_time=(time.time() - start) * 1000,
                success=result.returncode == 0
            )
            
        else:
            raise HTTPException(400, f"Язык {request.language} не поддерживается")
            
    except subprocess.TimeoutExpired:
        return CodeResponse(
            output="",
            error=f"Timeout ({request.timeout} sec)",
            execution_time=request.timeout * 1000,
            success=False
        )
    except Exception as e:
        return CodeResponse(
            output="",
            error=str(e),
            execution_time=(time.time() - start) * 1000,
            success=False
        )

@app.get("/health")
async def health():
    return {
        "status": "healthy",
        "languages": ["python", "javascript"]
    }

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000, workers=4)