FROM python:3-slim

RUN useradd -u 1000 -m zomboi
USER zomboi
WORKDIR /home/zomboi

COPY --chown=zomboi:zomboi requirements.txt ./
RUN pip install --user --no-cache-dir -r requirements.txt

COPY --chown=zomboi:zomboi . .
COPY --chown=zomboi:zomboi docker_config.py config.py

CMD [ "python", "zomboi.py" ]
