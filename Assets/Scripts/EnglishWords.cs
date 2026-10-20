// EnglishWords.cs
// ---------------------------------------------------------------------------
// The words enemies carry: about a thousand of the most common English words,
// the kind a typing test (e.g. Monkeytype's "english 1k") uses, so the game
// trains the words people actually type every day.
//
// Common is written in lowercase, separated by spaces and line breaks, in any
// order. WordBank uppercases them and sorts them by length:
//   3-4 letters  -> short words  (runners, normal zombies, frenzy)
//   5-7 letters  -> medium words (normal and armored zombies, shifters)
//   8-12 letters -> long words   (explosive zombies, boss limbs)
// Boss holds the long, hard ones for the boss's head (10-14 letters).
//
// To add words: type them into the right string, letters a-z only (no
// apostrophes, digits or capitals; TypingSettings adds capitals and symbols on the harder
// difficulties). Duplicates are ignored. Word families ("work", "worker")
// become WORD CHAINS automatically (WordBank.PickChainPair).
// ---------------------------------------------------------------------------
public static class EnglishWords
{
    public const string Common = @"
the and for are but not you all any can had her was one our out day get has him his how man new now old see two way who boy did its let put say she too use
also back been call came come each even find give good hand here home just keep kind know last left life like line live long look made make many more most move much must name need next only open over part play read real same seem show side some such sure take tell than that them then they this time turn very want well went were what when will with word work year your
ace act add age ago aid aim air arm art ask ate away bad bag ball band bank bar base bath bay bean bear beat bed bee bell belt best bike bill bird bit bite blue boat body bone book boot born boss both bowl box bus busy buy
cake calm camp cap car card care cart case cash cat cave cell chat chin city clap club coat code coin cold cook cool copy corn cost cow crab crew cry cup cut cute
dad dark data date dawn deal dear deep deer desk dial dice diet dig dish dive dog doll door dot down draw drew drop drum dry duck dust duty
ear earn ease east easy eat echo edge egg else end epic ever exit eye face fact fail fair fall fame fan far farm fast fat fear feed feel feet fell felt few file fill film fine fire firm fish fit five flag flat flew flip flow fly foam fog fold folk food fool foot form fort four free frog from fuel full fun fund
gain game gap gas gate gave gear gift girl glad glow glue goal goat gold golf gone grab gray grew grid grin grip grow gulf gum guy gym
hair half hall hang hard harm hat have head heal hear heat held help hero hide high hill hint hire hit hold hole hook hope horn host hot hour huge hug hunt hurt
ice icon idea inch into iron item ivy jam jar jazz jet job jog join joke joy jump just keen kept key kick kid king kiss kit kite knee knew knot
lab lack lady laid lake lamp land lane late law lawn lay lazy lead leaf lean leg lend less lid lift lime link lion lip list load loan lock log loop lose loss lost lot loud love low luck lung
mad mail main male mall map mark mask mass mat math may meal mean meat meet melt menu mess met mile milk mind mine miss mix mode mom moon mud mug myth
nail navy near neat neck nest net news nice nine node none noon nose note noun nut oak odd off oil okay once oral oven own owl
pace pack page paid pain pair palm pan park pass past path pay peak pen pet pick pie pig pile pin pine pink pipe plan plot plug plus poem poet pole pond pool poor pop port pose post pot pour pray pull pump pure push
quit quiz race rack rain rank rare rate raw rear red rely rent rest rice rich ride ring rise risk road rock role roll roof room root rope rose row rub rule run rush
sad safe said sail salt sand save saw sea seat seed seek seen self sell send set ship shoe shop shot shut sick sign silk sing sink sir sit site six size skin sky slip slow snow soap sock soft soil sold son song soon sort soul soup sour spin spot star stay step stir stop suit sum sun swim
tail tale talk tall tank tape task taxi tea team tear ten tend tent term test text thin tide tie till tiny tip tire toe told tone took tool top tour town toy tree trip true tube tune twin type
ugly unit upon urge used user van vast vary verb vet view vote wage wait wake walk wall war warm wash wave weak wear web week west whom why wide wife wild win wind wine wing wire wise wish wolf wood wool wore worm wrap
yard yarn yeah yell yes yet zero zone zoo
about above accept across action active actor actual adult advice afraid after again agent agree ahead allow almost alone along already always amazing amount angle angry animal annual answer anyone appear apple argue arrive artist aside attack author autumn avoid award aware
bakery balance basket battle beach beauty become before begin behind belief belong below better beyond bicycle bigger birth black blank blind block blood board bottle bottom brain branch brave bread break breath bridge brief bright bring broad broken brother brown budget build built burden butter button
cabin camera cancel candle canvas carbon career carpet carry castle casual catch caught cause center chain chair chance change chapter charge cheap check cheese chicken chief child choice choose church circle citizen claim class classic clean clear clever client climb clock close closed cloth cloud coach coast coffee collect color column comfort common company compare complex concert contain content contest control cookie corner cotton could count county couple courage course cousin cover craft crazy cream create credit crowd crown cruel culture curious current custom cycle
damage dance danger debate decade decide defend degree delay demand depend design desire detail device dinner direct doctor dollar double doubt dozen drama dream dress drink drive driver
eager early earth eight either elbow eleven email empty enemy energy engine enjoy enough enter entire equal escape essay event every exact exam example excite expect expert explain extra
fabric factor fairly faith false family famous fashion father fault favor feature fellow fence fever fiber field fifth fifty fight figure final finger finish first flavor flight floor flower focus follow force forest forget formal former forward found frame fresh friend front frozen fruit funny future
garage garden gather general gentle giant glass global golden grade grand grass great green ground group growth guard guess guest guide guitar
habit handle happen happy harbor health heart heavy height hello helpful hidden highway history hobby holiday honest honey horse hotel house human humor hundred hungry hunter hurry
image impact import include income indeed index inside insect island issue jacket jelly jewel joint journey judge juice jungle junior justice kettle kidney kindly kitchen knife knock
label labor ladder large laser later laugh launch lawyer layer leader learn least leather leave legal lemon length lesson letter level light limit liquid listen little lively living local lonely longer lovely lower lucky lunch
machine magic major manage manner market master match matter maybe meaning measure medal media medium member memory mental message metal method middle might minor minute mirror mistake mixture model modern moment money monkey month morning mother motion motor mouse mouth movie music myself
narrow nation native nature nearby nearly needle neither nerve never night noble noise normal north notice novel number nurse object obtain ocean offer office often orange order origin other outer outside owner oxygen
package painter palace panel paper parent party patient pattern peace pencil people pepper perfect period permit person phone photo piano picture piece pilot place plain planet plant plate player please plenty pocket point police polite popular potato powder power praise prefer press pretty price pride prince print prior prize problem process produce profit program proper proud prove public purple purpose puzzle
quality quarter queen quick quickly quiet quite quote rabbit radio raise random range rapid rather reach react ready reason recent record reduce refuse region relax remain remote remove repair repeat reply report rescue result return reveal review reward rhythm ribbon right river robot rocket rough round route royal rubber ruler runner rural
safety salad sample sauce scale scene school science score screen search season second secret section select seller senior sense series serve seven shadow shake shall shape share sharp sheet shelf shell shift shine shirt short should shout silent silver simple since single sister skill sleep slice slight small smart smile smoke smooth snake social society soldier solid solve sound source south space speak special speech speed spend spirit split sport spread spring square stable staff stage stair stand start state station steady steel stick still stone store storm story stove strange street stress strike string strong student study style subject sudden sugar summer sunny supply surface sweet swing symbol system
table talent target taste teach teacher temple tennis theory thick thing think third thirty though threat three throat throw ticket tiger timber title toast today tomato tongue tonight topic total touch tough toward tower track trade traffic train travel treat trend trial tribe truck trust truth twelve twenty
uncle under union unique unless until update upper upset usual valley value vector version victory video village visit visual vital voice volume voter
wagon waiter walker wallet wander water wealth weapon weather weekend weight welcome wheel where whether which while white whole widely window winner winter wisdom within without woman wonder wooden worker world worry worth would write writer wrong yellow yield young youth zebra
played player helped talked talker reader keeper caring careful hopeful hoped useful farmer hunted kindness painful runway sunset mindful handful playful
absolute academic accident activity actually addition advanced adventure advertise afternoon agreement airplane airport although ambition analysis animation announce anything anywhere apartment argument assistant atmosphere attention audience available
backpack backyard baseball bathroom beautiful beginning behavior birthday blackboard breakfast brilliant building business butterfly
calendar campaign capacity category celebrate ceremony champion chemistry children chocolate classroom collection colorful commercial community complete computer condition confident continue contract creative critical crossing curiosity customer
daughter deadline decision definitely delicious delivery describe designer detailed different difficult dinosaur direction discover discussion distance document dramatic
education effective election electric elephant elevator emotional employee encourage engineer enormous entrance envelope equipment everybody everyday everything everywhere evidence exactly exercise exchange existing expensive experience experiment explosion expression
familiar fantastic favorite festival financial fireworks football fountain frequency friendly friendship furniture
gardening generally generation gentleman geography gorgeous government graduate greatest guidance
handsome happiness hardware headline heritage highlight historical homework hospital household however hurricane
identity important impossible incident increase incredible indicate industry influence innocent insurance interest internet interview invention investment invisible
journalist keyboard knowledge landscape language laughter learning lifestyle limited listening literature location
magazine magnetic maintain majority management marathon material meanwhile medicine memorable midnight military minister mountain movement multiple musician
national natural navigation necessary negative neighbor newspaper notebook nutrition obviously occasion official operation opposite ordinary organize original otherwise overcome
painting paragraph passenger password peaceful perfectly personal pharmacy physical pineapple platform pleasant political population position positive possible practice president pressure previous princess priority probably producer progress property protection purchase
question reaction recently recommend recording relation relative religion remember research resource response restaurant
sandwich scenario schedule scientist seasonal security sentence separate serious shoulder sidewalk signature situation skateboard snowflake software solution something sometimes somewhere specific standard strategy strength structure struggle studying successful sunflower sunshine surprise swimming
teaspoon technical technology telephone telescope thousand together tomorrow tradition training treasure triangle umbrella underground understand universe university vacation valuable vegetable vertical volunteer
waterfall weakness wildlife wonderful workshop yesterday yourself
";

    // Compound words from the lists above, with a '|' between the two parts.
    // On Medium (and sometimes Hard / Extreme) they may be shown with a capital
    // on each part: "SunFlower", "WaterFall" (TypingSettings).
    public const string Compounds = @"
any|one high|way out|side week|end with|out run|way sun|set after|noon air|port any|thing any|where
back|pack back|yard base|ball bath|room birth|day black|board butter|fly class|room every|body every|day
every|thing every|where foot|ball friend|ship gentle|man head|line high|light home|work house|hold key|board
life|style news|paper note|book other|wise over|come pass|word pine|apple side|walk some|thing some|where
sun|flower sun|shine under|ground under|stand water|fall wild|life work|shop your|self air|plane dead|line
mid|night skate|board snow|flake soft|ware tea|spoon fire|works some|times hand|ful
";

    // The boss's head: long, everyday words that are a real test to spell.
    public const string Boss = @"
achievement advertisement appreciation architecture background celebration championship circumstance collaboration communication comprehensive concentration consideration construction contribution conversation demonstration determination development difference disappointment environment establishment examination experienced explanation extraordinary headquarters imagination immediately improvement independence information infrastructure installation instruction intelligence international interpretation introduction investigation manufacturing neighborhood observation opportunity organization participation particularly performance personality perspective philosophy photography preparation presentation professional pronunciation psychology recommendation refrigerator relationship representative requirement responsibility satisfaction significant temperature thunderstorm transportation understanding unfortunately vocabulary
";
}
