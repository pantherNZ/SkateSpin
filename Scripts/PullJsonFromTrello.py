import requests, json
import TrelloKey, ImportFromTrelloJson


# get board info
#url = f'https://api.trello.com/1/boards/{skatespin_board_id}'

# list boards
#url = 'https://api.trello.com/1/members/me/boards'
#url = url + f'?key={TrelloKey.key}&token={TrelloKey.token}'

# get full json data
base_url = 'https://api.trello.com/1/boards/{}?key={}&token={}&fields=all&actions=all&action_fields=all&actions_limit=1000&cards=all&card_fields=all&card_attachments=true&labels=all&lists=all&list_fields=all&members=all&member_fields=all&checklists=all&checklist_fields=all&organization=false'

boards = [
    ('617a6921b86d2d0a97572097', 'Manual Pad'),
    ('617a694152bdc05afe70a9f0', 'Ledge'),
    ('618f1a0b8e7f8936beba677f', 'Rail'),
    ('61769f73378de8262158689a', 'Vert'),
    ('617a6b11e6d60f7b4d9b2b04', 'Flat Ground'),
    ('617a6b359c4ddb448d6dadc9', 'Mini Ramp'),
]

for board, name in boards:
    url = base_url.format(board, TrelloKey.key, TrelloKey.token)

    response = requests.request("GET",url)

    file = open(f'JSON/{name}.json', 'w+')
    file.write(response.text)
    print(f'Finished writing {name}.json')
    #json_data = json.loads(response.text)
    #file.write(json.dumps(json_data, indent=4, sort_keys=True))

print(f'Starting JSON import to table data')
ImportFromTrelloJson.import_from_json()