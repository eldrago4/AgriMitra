FROM python:3.11-slim

WORKDIR /app

# Install system dependencies
RUN apt-get update && apt-get install -y --no-install-recommends \
    libgomp1 \
    && rm -rf /var/lib/apt/lists/*

# Install Python dependencies
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Copy service
COPY inference_service.py .

# Model file is mounted via docker-compose volume
# COPY agrimitra_int8.onnx .

EXPOSE 8001

CMD ["uvicorn", "inference_service:app", "--host", "0.0.0.0", "--port", "8001", "--workers", "2"]
