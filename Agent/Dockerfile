FROM python:3.13-slim

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1

WORKDIR /app
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt \
 && useradd --system --uid 10001 --no-create-home --shell /usr/sbin/nologin agent \
 && mkdir -p /data && chown 10001:10001 /data
# /data belongs to the agent so a fresh named volume (local agent next to the Monitorer) inherits the owner

COPY ssm_agent/ ./ssm_agent/

# Runs unprivileged: only needs read access to /host/proc + /hostfs and write access to /data
USER 10001:10001
ENTRYPOINT ["python", "-m", "ssm_agent", "--config", "/config/config.yml"]
