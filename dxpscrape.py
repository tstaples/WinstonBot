import requests
from bs4 import BeautifulSoup

grape = [
        "Shanelle",
        "Situations",
        "NeoNerV",
        "Kadeem",
        "IceRiver225",
        "Gamie",
        "brianward23",
        "Hoffster",
        "Captain Dk53",
        "WARL0RD TH0R",
        "Old_fally",
        "yu-sin-kwan"
]

cherry = [
        "Batsie",
        "FeralCreator",
        "Ghost Gob",
        "Nexxey",
        "TuggyMcNutty",
        "Ody29",
        "doe gewoon",
        "yare_bear",
        "XxKrazinoxX",
        "Vws dipper",
        "ItsGarfield",
        "Nugget815"
]

apple = [
        "Catman",
        "Rubiess",
        "hidenpequin",
        "Blaviken",
        "K1ngchile69",
        "MeleeNewb",
        "Feerip",
        "PepperSaltYo",
        "Finnsisjon",
        "Sinteresting",
        "Abyssal Arse",
        "ohitskirsten"
]

peach = [
        "9tails",
        "Walkers",
        "EatMyBabyz",
        "woefulsteve",
        "Fineapples",
        "Beauty4Ashes",
        "Ayhet",
        "PecorineChan",
        "Matar",
        "Sir Tobias",
        "GW143",
        "Demi3k"
]

def getUserXp(name):
        sanitizedName = name.replace(' ', '+')
        url = "https://www.runeclan.com/user/%s" % sanitizedName
        payload = {
                "dxp_col" : "dxp"
        }

        response = requests.post(url, data=payload)
        if response == None:
                raise Exception("Failed to get data for user %s" % name)
        html = response.text
        soup = BeautifulSoup(html, features="html.parser")
        xp = soup.find("td", "xp_tracker_gain xp_tracker_pos")
        if xp == None:
                print("Failed to find xp element for %s, they probably have 0 xp" % name)
                return "0"
        return xp.get_text()

def tallyTeam(team):
        teamXp = []
        totalxp = 0
        for rsn in team:
                xp = getUserXp(rsn)
                print("%s - %s" % (rsn, xp))
                xpInt = int(xp.replace(',',''))
                totalxp = totalxp + xpInt
                teamXp.append((rsn, xpInt))

        teamXp.sort(key=lambda tup: tup[1], reverse=True)
        return totalxp, teamXp

def formatResults(teamName, total, indivials):
        formattedTotal = '{:,}'.format(total)
        lineSep = "---------------------------------------------\n"

        output = "```\n"
        output = output + lineSep
        output = output + "Team " + teamName + " - " + formattedTotal + "\n"
        output = output + lineSep
        for pair in indivials:
                formattedName = '{0: <20}'.format(pair[0])
                formattedXp = '{:,}'.format(pair[1])
                output = output + formattedName + " " + formattedXp + "\n"
        output = output + lineSep
        output = output + "```"
        return output

grapeTotal, grapeIndivial = tallyTeam(grape)
appleTotal, appleIndivial = tallyTeam(apple)
cherryTotal, cherryIndivial = tallyTeam(cherry)
peachTotal, peachIndivial = tallyTeam(peach)

grapeFormatted = formatResults("Grape", grapeTotal, grapeIndivial)
appleFormatted = formatResults("Apple", appleTotal, appleIndivial)
cherryFormatted = formatResults("Cherry", cherryTotal, cherryIndivial)
peachFormatted = formatResults("Peach", peachTotal, peachIndivial)

allFormatted = grapeFormatted + appleFormatted + cherryFormatted + peachFormatted
